# 真编译验收脚本（v1.1.43）：
# 用 Unity 自带 Roslyn（DotNetSdk）+ Library/ScriptAssemblies 依赖 DLL + UnityEngine 模块 + Mono BCL
# 对 Assets/Scripts/now_use（Game 程序集）做真实类型级编译（csc 响应文件，绕开 32K 命令行上限）。
# 阳性对照已验证：注入 HashSet.AddRange 等错误必被抓出（防"假通过"）。
# 用法：python _dev/compile_check.py   退出码 0=通过 / 1=有编译错误 / 2=检查器自身异常
import subprocess, glob, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNITY = r"E:/WeGameApps/6000.5.4f1/Editor/Data"
CSC = UNITY + "/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll"
DOTNET = UNITY + "/DotNetSdk/dotnet.exe"   # Unity 自带 dotnet（系统 dotnet 运行时过旧）

src = glob.glob(os.path.join(ROOT, "Assets/Scripts/now_use/**/*.cs"), recursive=True)
if not src:
    print("no sources"); sys.exit(2)

refs = []
refs += glob.glob(UNITY + "/Managed/UnityEngine/UnityEngine.*Module.dll")
refs += [UNITY + "/Managed/UnityEngine/UnityEngine.dll", UNITY + "/Managed/UnityEngine/UnityEditor.dll"]   # 类型转发面
bcl = UNITY + "/MonoBleedingEdge/lib/mono/4.7.1-api"
if not os.path.isdir(bcl): bcl = UNITY + "/MonoBleedingEdge/lib/mono/4.5-api"
# BCL：Mono 4.7.1 profile 三主库 + Facades（HashSet 容量构造等 Unity 实际 API 面）
refs += [os.path.join(bcl, f) for f in ("mscorlib.dll", "System.dll", "System.Core.dll")]
refs += glob.glob(bcl + "/Facades/*.dll")
# UnityEditor（MenuItem 在 CoreModule；Preserve 在 Unity.Scripting）
refs += glob.glob(UNITY + "/Managed/UnityEngine/UnityEditor.*Module.dll")
refs += glob.glob(UNITY + "/Managed/UnityEngine/Unity.*.dll")   # Unity.Scripting 等非 Module 命名程序集
# 预编译插件（DOTween 等在 Assets/Plugins）
refs += glob.glob(os.path.join(ROOT, "Assets/Plugins/**/*.dll"), recursive=True)
refs += glob.glob(os.path.join(ROOT, "Library/ScriptAssemblies/*.dll"))
refs = [r for r in refs if not r.endswith("Game.dll")]   # 排除旧自程序集（防新旧类型混判）

rsp = os.path.join(ROOT, "Temp/_dev_compile.rsp")
os.makedirs(os.path.dirname(rsp), exist_ok=True)
def slash(p): return p.replace(chr(92), "/")
with open(rsp, "w", encoding="utf-8") as f:
    f.write("-nologo\n-target:library\n-nostdlib+\n-define:UNITY_EDITOR\n")
    f.write("-nowarn:CS0414,CS0649,CS0169,CS0219\n")
    f.write("-out:" + slash(os.path.join(ROOT, "Temp/_dev_compile_check.dll")) + "\n")
    for r in refs:
        f.write('-r:"' + slash(r) + '"\n')
    for c in src:
        f.write('"' + slash(c) + '"\n')

p = subprocess.run([DOTNET, CSC, "@" + slash(rsp)], capture_output=True, cwd=ROOT, timeout=300)
out = ((p.stdout or b"") + (p.stderr or b"")).decode("utf-8", errors="replace")
lines = [l for l in out.splitlines() if l.strip() and not l.startswith("Microsoft (R)")]
errs = [l for l in lines if "error CS" in l]
warns = [l for l in lines if "warning CS" in l and "now_use" in l]

print("源文件 %d 个 | 引用 %d 个 DLL | csc 退出码 %d" % (len(src), len(refs), p.returncode))
if p.returncode not in (0, 1) or (p.returncode == 1 and not errs and not out.strip()):
    # 编译器没跑起来 / 静默失败——绝不能报"通过"
    print("==== 检查器异常（编译器未正常执行）====")
    print(out[:800])
    sys.exit(2)
if errs:
    print("==== 编译错误 %d 条 ====" % len(errs))
    for e in errs[:40]:
        print(e)
    sys.exit(1)
print("==== 编译通过：0 错误 ====")
if warns:
    print("警告 %d 条（前 10）：" % len(warns))
    for w in warns[:10]:
        print(w)
sys.exit(0)
