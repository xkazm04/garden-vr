# tools/gen3d - local image-to-3D (T-TER-046, dossier F5)

ComfyUI and the venv live OUTSIDE the repo in `C:\ai` (about 12 GB of weights under `C:\ai\ComfyUI\models`).

## Install (Windows, RTX 4090, what actually worked on 2026-10-04)

```
python -m venv C:\ai\venv
C:\ai\venv\Scripts\python -m pip install torch torchvision --index-url https://download.pytorch.org/whl/cu128   # 2.11.0+cu128
git clone --depth 1 https://github.com/comfyanonymous/ComfyUI C:\ai\ComfyUI                                    # f1072eb, 2026-10-03
git clone --depth 1 https://github.com/PozzettiAndrea/ComfyUI-TRELLIS2 C:\ai\ComfyUI\custom_nodes\ComfyUI-TRELLIS2  # 3e696f2, 2026-07-31, MIT
C:\ai\venv\Scripts\python -m pip install -r C:\ai\ComfyUI\requirements.txt -r C:\ai\ComfyUI\custom_nodes\ComfyUI-TRELLIS2\requirements.txt
# the node's own install.py builds a pixi env and fails on Windows (it resolves tokenizers==0.10.3, no wheel, Rust build error).
# Install the same deps and the cu128/torch2.11/cp312 CUDA wheels into the host venv instead:
cd C:\ai; C:\ai\venv\Scripts\python <repo>\tools\gen3d\install_trellis_host.py
# a failed install.py leaves a partial pixi env that makes the node try (and fail) to scan in isolation; move it away:
move %LOCALAPPDATA%\Programs\comfy-env\.pixi\envs %LOCALAPPDATA%\Programs\comfy-env\.pixi\envs_failed
# one upstream bug against this ComfyUI build (SparseStructureFlowModel reads self.device before it has parameters):
cd C:\ai\ComfyUI\custom_nodes\ComfyUI-TRELLIS2; git apply <repo>\tools\gen3d\patches\trellis2-device-arg.patch
```

Weights (`microsoft/TRELLIS.2-4B`, DINOv3 conditioner, BiRefNet) download from Hugging Face on the first run (about 10 min at 10 MB/s).

## Run

```
cd C:\ai\ComfyUI; C:\ai\venv\Scripts\python main.py --port 8188 --disable-auto-launch      # leave running
python tools/gen3d/gen.py <image.png> <out.glb> --seed 42 --res 1024_cascade               # one prop; writes <out>.gen.json
bash tools/gen3d/arena_local.sh "42 7" mushroom moss_clump pebble_set                      # the arena arm
blender -b -P tools/blender/finish_generated.py -- --in <out.glb> --class mushroom --tris 1200 --size-cm 3 --remesh voxel --out <dir>
python tools/gen3d/contact_sheet.py                                                        # evidence sheet
```

`tripo.py` is the paid arm (credit-guarded, ledger in `tripo_ledger.jsonl`); it needs `TRIPO_API_KEY` in `.env`.
