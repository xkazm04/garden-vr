using System;
using System.Collections.Generic;
using System.IO;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

public class SaveCrashWindowTests
{
    // Save rotates live -> prev1 and only then moves the finished temp file into place. A crash between those
    // two moves leaves no live file but a complete temp file holding the newest save. Load must recover it
    // instead of reporting "missing-save" and offering a backup that is one save behind.

    [Fact]
    public void A_crash_between_rotating_the_live_file_and_installing_the_temp_file_loses_nothing()
    {
        var io = new MemoryIo();
        var store = Store(io);
        store.Save(new Note { Name = "first" });
        store.Save(new Note { Name = "second" });

        io.FailMoveInto = SaveStore<Note>.LiveName;
        Assert.Throws<IOException>(() => store.Save(new Note { Name = "third" }));
        io.FailMoveInto = null;
        Assert.False(io.Exists(SaveStore<Note>.LiveName));

        var loaded = Store(io).Load();
        Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);
        Assert.Equal("third", loaded.Doc.Name);
    }

    [Fact]
    public void A_missing_live_file_with_no_temp_file_is_still_a_failure_with_a_backup()
    {
        var io = new MemoryIo();
        var store = Store(io);
        store.Save(new Note { Name = "first" });
        store.Save(new Note { Name = "second" });
        io.Delete(SaveStore<Note>.LiveName);

        var loaded = Store(io).Load();
        Assert.Equal(LoadOutcome.Failed, loaded.Outcome);
        Assert.Equal("missing-save", loaded.FailedStep);
        Assert.True(loaded.BackupAvailable);
    }

    [Fact]
    public void A_torn_temp_file_from_a_first_ever_save_still_loads_fresh()
    {
        var io = new MemoryIo();
        using (var torn = io.Create(SaveStore<Note>.TempName)) { torn.Write(new byte[] { 0x7b, 0x22, 0x53 }, 0, 3); }

        var loaded = Store(io).Load();
        Assert.Equal(LoadOutcome.Fresh, loaded.Outcome);
        Assert.False(io.Exists(SaveStore<Note>.LiveName));
    }

    static SaveStore<Note> Store(ISaveIo io)
    {
        return new SaveStore<Note>(
            io,
            1,
            () => new Note { Name = "" },
            obj => new Note { Name = obj.Get("Name").AsString() },
            doc =>
            {
                var obj = new JsonObject();
                obj.Set("SchemaVersion", JsonValue.Number(1));
                obj.Set("Name", JsonValue.String(doc.Name));
                return obj;
            },
            Array.Empty<MigrationStep>());
    }

    sealed class Note { public string Name; }

    internal sealed class MemoryIo : ISaveIo
    {
        readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>();
        public string FailMoveInto;

        public bool Exists(string name) { return _files.ContainsKey(name); }
        public byte[] ReadBytes(string name) { return _files[name]; }
        public Stream Create(string name) { return new Sink(this, name); }
        public void Delete(string name) { _files.Remove(name); }
        public void Copy(string from, string to) { _files[to] = _files[from]; }

        public void Move(string from, string to)
        {
            if (to == FailMoveInto) throw new IOException("simulated crash");
            if (_files.ContainsKey(to)) throw new IOException("refusing to overwrite " + to);
            _files[to] = _files[from];
            _files.Remove(from);
        }

        sealed class Sink : MemoryStream
        {
            readonly MemoryIo _io;
            readonly string _name;
            public Sink(MemoryIo io, string name) { _io = io; _name = name; }
            protected override void Dispose(bool disposing)
            {
                if (disposing) _io._files[_name] = ToArray();
                base.Dispose(disposing);
            }
        }
    }
}
