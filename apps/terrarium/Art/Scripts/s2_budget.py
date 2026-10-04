"""Spike S2 (T-TER-044): print the measure-*.json files of a run folder as one table."""
import glob, json, os, sys
folder = sys.argv[1] if len(sys.argv) > 1 else os.path.join("orchestration", "runs", "terrarium", "T-TER-044")
print("| look | draws | tris | transparent mean layers | transparent coverage |")
print("| --- | --- | --- | --- | --- |")
for f in sorted(glob.glob(os.path.join(folder, "measure-*.json"))):
    j = json.load(open(f))
    print("| %s | %s | %s | %.4f | %.3f |" % (os.path.basename(f)[8:-5], j["drawsEst"], j["tris"], j["transparentMeanLayers"], j["transparentCoverage"]))
