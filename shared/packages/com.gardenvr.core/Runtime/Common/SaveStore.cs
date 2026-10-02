using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GardenVR.Core
{
    public enum LoadOutcome { Fresh, Loaded, Migrated, Failed }

    public sealed class LoadResult<T>
    {
        public LoadOutcome Outcome;
        public T Doc;
        public string FailedStep;
        public bool BackupAvailable;
        /// <summary>True when the file's schema is newer than this build. <see cref="ISaveStore{T}.Save"/> then refuses.</summary>
        public bool ReadOnly;
    }

    public interface ISaveStore<T> where T : class
    {
        LoadResult<T> Load();
        void Save(T doc);
    }

    /// <summary>One append-only step from <see cref="FromVersion"/> to the next integer. Never edit a step that has shipped.</summary>
    public sealed class MigrationStep
    {
        public int FromVersion { get; }
        public string Id { get; }
        public Action<JsonObject> Apply { get; }

        public MigrationStep(int fromVersion, string id, Action<JsonObject> apply)
        {
            if (fromVersion < 1) throw new ArgumentOutOfRangeException(nameof(fromVersion));
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("migration id");
            if (apply == null) throw new ArgumentNullException(nameof(apply));
            FromVersion = fromVersion;
            Id = id;
            Apply = apply;
        }
    }

    /// <summary>
    /// File port for <see cref="SaveStore{T}"/>. Tests wrap <see cref="Create"/> with a stream that fails mid-write.
    /// Names are file names in one directory, not paths.
    /// </summary>
    public interface ISaveIo
    {
        bool Exists(string name);
        byte[] ReadBytes(string name);
        Stream Create(string name);
        void Move(string from, string to);
        void Delete(string name);
        void Copy(string from, string to);
    }

    public sealed class DiskSaveIo : ISaveIo
    {
        readonly string _dir;
        /// <summary>Optional wrap of the write stream. A wrapper that throws leaves the live file untouched.</summary>
        public Func<Stream, Stream> WrapWrite;

        public DiskSaveIo(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("directory");
            _dir = directory;
            Directory.CreateDirectory(_dir);
        }

        public bool Exists(string name) { return File.Exists(Full(name)); }

        public byte[] ReadBytes(string name) { return File.ReadAllBytes(Full(name)); }

        public Stream Create(string name)
        {
            Stream stream = new FileStream(Full(name), FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
            if (WrapWrite != null) stream = WrapWrite(stream);
            return stream;
        }

        public void Move(string from, string to)
        {
            var dst = Full(to);
            if (File.Exists(dst)) throw new IOException("refusing to overwrite " + to);
            File.Move(Full(from), dst);
        }

        public void Delete(string name)
        {
            var path = Full(name);
            if (File.Exists(path)) File.Delete(path);
        }

        public void Copy(string from, string to)
        {
            var dst = Full(to);
            if (File.Exists(dst)) throw new IOException("refusing to overwrite " + to);
            File.Copy(Full(from), dst);
        }

        string Full(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0 || name.IndexOf(':') >= 0)
                throw new ArgumentException("save name must be a single file name");
            return Path.Combine(_dir, name);
        }
    }

    /// <summary>
    /// Atomic save: write <c>save.json.tmp</c>, flush, rotate <c>save.json</c> to <c>save.prev1.json</c>
    /// and that file to <c>save.prev2.json</c>, then move the temp file into place.
    /// A newer <c>SchemaVersion</c> loads read-only. <see cref="LoadOutcome.Failed"/> is never <see cref="LoadOutcome.Fresh"/>.
    /// </summary>
    public sealed class SaveStore<T> : ISaveStore<T> where T : class
    {
        public const string LiveName = "save.json";
        public const string TempName = "save.json.tmp";
        public const string Prev1Name = "save.prev1.json";
        public const string Prev2Name = "save.prev2.json";
        public const string SnapshotName = "save.snapshot.json";

        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        readonly ISaveIo _io;
        readonly int _currentSchema;
        readonly Func<T> _createFresh;
        readonly Func<JsonObject, T> _read;
        readonly Func<T, JsonObject> _write;
        readonly MigrationStep[] _steps;
        bool _readOnly;

        public SaveStore(
            ISaveIo io,
            int currentSchema,
            Func<T> createFresh,
            Func<JsonObject, T> read,
            Func<T, JsonObject> write,
            IReadOnlyList<MigrationStep> migrations)
        {
            if (io == null) throw new ArgumentNullException(nameof(io));
            if (currentSchema < 1) throw new ArgumentOutOfRangeException(nameof(currentSchema));
            if (createFresh == null) throw new ArgumentNullException(nameof(createFresh));
            if (read == null) throw new ArgumentNullException(nameof(read));
            if (write == null) throw new ArgumentNullException(nameof(write));
            _io = io;
            _currentSchema = currentSchema;
            _createFresh = createFresh;
            _read = read;
            _write = write;
            int count = migrations == null ? 0 : migrations.Count;
            _steps = new MigrationStep[count];
            int expected = 1;
            for (int i = 0; i < count; i++)
            {
                var step = migrations[i];
                if (step == null) throw new ArgumentException("null migration step");
                if (step.FromVersion != expected)
                    throw new ArgumentException("migration steps are an append-only chain starting at version 1");
                _steps[i] = step;
                expected++;
            }
            if (count > 0 && currentSchema != expected)
                throw new ArgumentException("current schema must be the version after the last migration step");
        }

        public LoadResult<T> Load()
        {
            _readOnly = false;
            if (!_io.Exists(LiveName))
            {
                if (Backup()) return Fail("missing-save");
                return new LoadResult<T>
                {
                    Outcome = LoadOutcome.Fresh,
                    Doc = _createFresh(),
                    FailedStep = null,
                    BackupAvailable = false,
                    ReadOnly = false
                };
            }

            string text;
            try
            {
                text = Utf8.GetString(_io.ReadBytes(LiveName));
                if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
            }
            catch (Exception) { return Fail("read"); }

            JsonObject obj;
            try { obj = Json.ParseObject(text); }
            catch (Exception) { return Fail("parse"); }

            int ver;
            try { ver = RequireVersion(obj); }
            catch (Exception) { return Fail("schema-version"); }

            if (ver > _currentSchema)
            {
                T newer;
                try { newer = _read(obj); }
                catch (Exception) { return Fail("read-doc"); }
                _readOnly = true;
                return new LoadResult<T>
                {
                    Outcome = LoadOutcome.Loaded,
                    Doc = newer,
                    FailedStep = null,
                    BackupAvailable = Backup(),
                    ReadOnly = true
                };
            }

            if (ver < _currentSchema)
            {
                try
                {
                    if (!_io.Exists(SnapshotName)) _io.Copy(LiveName, SnapshotName);
                }
                catch (Exception) { return Fail("snapshot"); }

                int cursor = ver;
                for (int i = 0; i < _steps.Length; i++)
                {
                    var step = _steps[i];
                    if (step.FromVersion < cursor) continue;
                    if (step.FromVersion != cursor) return Fail("missing-step-" + cursor.ToString());
                    try { step.Apply(obj); }
                    catch (Exception) { return Fail(step.Id); }
                    cursor++;
                    obj.Set("SchemaVersion", JsonValue.Number(cursor));
                }
                if (cursor != _currentSchema) return Fail("missing-step-" + cursor.ToString());
                try { WriteObjectAtomic(obj); }
                catch (Exception) { return Fail("write-migrated"); }
                T migrated;
                try { migrated = _read(obj); }
                catch (Exception) { return Fail("read-doc"); }
                return new LoadResult<T>
                {
                    Outcome = LoadOutcome.Migrated,
                    Doc = migrated,
                    FailedStep = null,
                    BackupAvailable = Backup(),
                    ReadOnly = false
                };
            }

            T doc;
            try { doc = _read(obj); }
            catch (Exception) { return Fail("read-doc"); }
            return new LoadResult<T>
            {
                Outcome = LoadOutcome.Loaded,
                Doc = doc,
                FailedStep = null,
                BackupAvailable = Backup(),
                ReadOnly = false
            };
        }

        public void Save(T doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (_readOnly)
                throw new InvalidOperationException("This save was written by a newer version and is read-only.");
            var obj = _write(doc);
            if (obj == null) throw new InvalidOperationException("write returned no document");
            int ver = RequireVersion(obj);
            if (ver > _currentSchema)
                throw new InvalidOperationException(
                    "Refusing to write schema " + ver.ToString() + "; this build understands " + _currentSchema.ToString() + ".");
            WriteObjectAtomic(obj);
        }

        void WriteObjectAtomic(JsonObject obj)
        {
            var bytes = Utf8.GetBytes(Json.Write(obj));
            using (var stream = _io.Create(TempName))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
            if (_io.Exists(Prev2Name)) _io.Delete(Prev2Name);
            if (_io.Exists(Prev1Name)) _io.Move(Prev1Name, Prev2Name);
            if (_io.Exists(LiveName)) _io.Move(LiveName, Prev1Name);
            _io.Move(TempName, LiveName);
        }

        bool Backup()
        {
            return _io.Exists(Prev1Name) || _io.Exists(Prev2Name) || _io.Exists(SnapshotName);
        }

        LoadResult<T> Fail(string step)
        {
            _readOnly = false;
            return new LoadResult<T>
            {
                Outcome = LoadOutcome.Failed,
                Doc = default(T),
                FailedStep = step,
                BackupAvailable = Backup(),
                ReadOnly = false
            };
        }

        static int RequireVersion(JsonObject obj)
        {
            if (obj == null || !obj.Has("SchemaVersion")) throw new FormatException("missing SchemaVersion");
            return obj.Get("SchemaVersion").AsInt();
        }
    }
}
