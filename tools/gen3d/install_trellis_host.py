# Host-venv install of the TRELLIS2 node deps, bypassing comfy-env's pixi env (its resolver picks tokenizers==0.10.3, no wheel).
import subprocess, sys
from comfy_env.packages.cuda_wheels import get_wheel_url
urls=[]
for p in ["cumesh","flex_gemm_ap","cumesh_vb","o_voxel_vb_ap","flash-attn","sageattention","drtk"]:
    u=get_wheel_url(p,"2.11.0","12.8","312"); print(p,u); urls.append(u)
pip=[sys.executable,"-m","pip","install","--only-binary=:all:"]
subprocess.check_call(pip+["--no-deps"]+[u for u in urls if u])
subprocess.check_call(pip+["huggingface_hub","hf_transfer","hf_xet","safetensors","timm","plyfile","zstandard","opencv-python-headless","imageio","easydict","triton-windows>=3.5.0","trimesh[easy]","comfy-sparse-attn","comfy-kitchen","comfy-aimdo","kornia"])
