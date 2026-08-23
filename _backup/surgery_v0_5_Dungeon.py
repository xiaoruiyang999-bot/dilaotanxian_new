# -*- coding: utf-8 -*-
"""v0_5_Dungeon.unity 场景手术（v0.5.4.4.4）：
1. 恢复 DungeonSystem 上丢失的 RunManager 组件（从 HEAD 版本找回）
2. 删除调试残留：Debug_Player / Debug_RealPlayer / Debug_EnemyRanged 及全部子树、7 个泄漏 DamagePopup
3. 新建指向正式 Player prefab 实例的 stripped Transform 引用，
   重接 CameraFollow.target 与 DungeonManager.player（原先均为 fileID: 0）
4. 引用完整性验证：保留块中不得再引用被删 fileID
"""
import re
import sys

PATH = 'Assets/Scenes/v0_5_Dungeon.unity'
CAMERA_GUID = '5bcb46c3dfd0b4c43998282c3515b3d2'   # CameraFollow.cs
PLAYER_INSTANCE = 2002241262                        # 场景中正式 Player prefab 实例
PLAYER_ROOT_TRANSFORM = 857263768900391870          # Player.prefab 根 Transform
PLAYER_PREFAB_GUID = '4ab53a63dc162b74a9446808f247efa6'
STRIPPED_TF = 1474215305                            # HEAD 中用过的 stripped Transform fileID，当前空闲
RUNMANAGER_BLOCK = (
    '--- !u!114 &1357356021\n'
    'MonoBehaviour:\n'
    '  m_ObjectHideFlags: 0\n'
    '  m_CorrespondingSourceObject: {fileID: 0}\n'
    '  m_PrefabInstance: {fileID: 0}\n'
    '  m_PrefabAsset: {fileID: 0}\n'
    '  m_GameObject: {fileID: 1357356017}\n'
    '  m_Enabled: 1\n'
    '  m_EditorHideFlags: 0\n'
    '  m_Script: {fileID: 11500000, guid: 1e0012b16c65ca24c8900b966b58dd77, type: 3}\n'
    '  m_Name: \n'
    '  m_EditorClassIdentifier: Assembly-CSharp::RunManager\n'
    '  dungeonManager: {fileID: 1357356020}\n'
    '  rewardChestPrefab: {fileID: 6358551137603142388, guid: aa0f79463f13b324c93fe34b6346b1ed, type: 3}\n'
    '  portalPrefab: {fileID: 2406936545735521585, guid: 8c98270872baf6d408c9ca9c7fec7de5, type: 3}\n'
    '  restartDelay: 2\n'
)
STRIPPED_BLOCK = (
    '--- !u!4 &{tf} stripped\n'
    'Transform:\n'
    '  m_CorrespondingSourceObject: {{fileID: {root}, guid: {guid}, type: 3}}\n'
    '  m_PrefabInstance: {{fileID: {inst}}}\n'
    '  m_PrefabAsset: {{fileID: 0}}\n'
).format(tf=STRIPPED_TF, root=PLAYER_ROOT_TRANSFORM, guid=PLAYER_PREFAB_GUID, inst=PLAYER_INSTANCE)

with open(PATH, encoding='utf-8', newline='') as f:
    content = f.read()

pattern = re.compile(r'^--- !u!(\d+) &(\d+)( stripped)?$', re.M)
matches = list(pattern.finditer(content))
header = content[:matches[0].start()]

blocks = []
for i, m in enumerate(matches):
    end = matches[i + 1].start() if i + 1 < len(matches) else len(content)
    blocks.append([int(m.group(2)), int(m.group(1)), bool(m.group(3)), content[m.start():end]])
block_map = {b[0]: b for b in blocks}

# ---------- 1. 收集删除集合 ----------
to_delete = set()

def collect_go(go_fid):
    if go_fid in to_delete:
        return
    to_delete.add(go_fid)
    body = block_map[go_fid][3]
    for c in re.findall(r'component: \{fileID: (\d+)\}', body):
        c = int(c)
        to_delete.add(c)
        if c not in block_map:
            continue
        cbody = block_map[c][3]
        if block_map[c][1] == 4 and 'm_Children:' in cbody:  # Transform: 递归子树
            seg = cbody.split('m_Children:')[1].split('m_Father:')[0]
            for ch in re.findall(r'\{fileID: (\d+)\}', seg):
                ch = int(ch)
                to_delete.add(ch)
                if ch in block_map:
                    child_go = re.search(r'm_GameObject: \{fileID: (\d+)\}', block_map[ch][3])
                    if child_go:
                        collect_go(int(child_go.group(1)))

for root in (881835298, 1239041967, 2088072185,            # 三个 Debug_ 对象
             114604238, 1307531374, 1342013004, 1407985414,
             1774473345, 1860083635, 2107352226):           # 7 个泄漏 DamagePopup
    collect_go(root)
print(f'[1] 删除集合：{len(to_delete)} 个块')

# ---------- 1.5. SceneRoots 列表清理（被删对象均为场景根，须同步移出根列表） ----------
SCENE_ROOTS = 9223372036854775807
roots_body = block_map[SCENE_ROOTS][3]
lines = roots_body.split('\n')
kept_lines = [ln for ln in lines
              if not (ln.strip().startswith('- {fileID:')
                      and int(re.search(r'\{fileID: (\d+)\}', ln).group(1)) in to_delete)]
removed_roots = len(lines) - len(kept_lines)
block_map[SCENE_ROOTS][3] = '\n'.join(kept_lines)
for b in blocks:
    if b[0] == SCENE_ROOTS:
        b[3] = block_map[SCENE_ROOTS][3]
print(f'[1.5] SceneRoots：移除 {removed_roots} 个根引用')

# ---------- 2. 修改保留块 ----------
def patch_block(fid, old, new, must=True):
    b = block_map[fid]
    if old not in b[3]:
        if must:
            print(f'  [失败] 块 &{fid} 中找不到: {old!r}')
            sys.exit(1)
        return False
    b[3] = b[3].replace(old, new, 1)
    return True

# 2a. DungeonSystem 组件列表补 RunManager
patch_block(1357356017,
            '  - component: {fileID: 1357356020}\n',
            '  - component: {fileID: 1357356020}\n  - component: {fileID: 1357356021}\n')
print('[2a] DungeonSystem.m_Component 已补回 RunManager')

# 2b. DungeonManager.player 接到正式 Player 实例
patch_block(1357356020, '  player: {fileID: 0}\n', f'  player: {{fileID: {STRIPPED_TF}}}\n')
print('[2b] DungeonManager.player 已重接')

# 2c. CameraFollow.target 接到正式 Player 实例
cam_fid = None
for fid, cls, stripped, body in blocks:
    if cls == 114 and CAMERA_GUID in body and fid not in to_delete:
        cam_fid = fid
        break
if cam_fid is None:
    print('  [失败] 找不到 CameraFollow 组件块')
    sys.exit(1)
patch_block(cam_fid, '  target: {fileID: 0}\n', f'  target: {{fileID: {STRIPPED_TF}}}\n')
print(f'[2c] CameraFollow(&{cam_fid}).target 已重接')

# ---------- 3. 重建文件（保留块顺序 + 两个新块插入相关位置） ----------
out = [header]
runmanager_inserted = stripped_inserted = False
for fid, cls, stripped_flag, body in blocks:
    if fid in to_delete:
        continue
    out.append(body)
    if fid == 1357356020:                     # DungeonManager 块后插 RunManager
        out.append(RUNMANAGER_BLOCK)
        runmanager_inserted = True
    if fid == PLAYER_INSTANCE:                # Player PrefabInstance 块后插 stripped Transform
        out.append(STRIPPED_BLOCK)
        stripped_inserted = True
if not runmanager_inserted:
    out.append(RUNMANAGER_BLOCK)
if not stripped_inserted:
    out.append(STRIPPED_BLOCK)
new_content = ''.join(out)
with open(PATH, 'w', encoding='utf-8', newline='') as f:
    f.write(new_content)
print(f'[3] 写回完成：{len(blocks)} 块 -> {len(blocks) - len([f for f in to_delete if f in block_map]) + 2} 块')

# ---------- 4. 引用完整性验证 ----------
with open(PATH, encoding='utf-8', newline='') as f:
    verify = f.read()
vmatches = list(pattern.finditer(verify))
vblocks = []
for i, m in enumerate(vmatches):
    end = vmatches[i + 1].start() if i + 1 < len(vmatches) else len(verify)
    vblocks.append((int(m.group(2)), verify[m.start():end]))
dangling = []
kept = {fid for fid, _ in vblocks}
for fid, body in vblocks:
    for ref in re.findall(r'\{fileID: (\d+)\}', body):
        ref = int(ref)
        if ref in to_delete and ref != fid:
            dangling.append((fid, ref))
if dangling:
    print('  [警告] 悬空引用：')
    for fid, ref in dangling:
        print(f'    块 &{fid} 引用了被删除的 &{ref}')
    sys.exit(1)
print('[4] 引用完整性通过：无悬空引用')

# ---------- 5. 最终断言 ----------
assert verify.count('1e0012b16c65ca24c8900b966b58dd77') == 1, 'RunManager guid 计数异常'
assert verify.count('1357356021') == 2, 'RunManager fileID 计数异常'
assert 'Debug_Player' not in verify and 'Debug_RealPlayer' not in verify and 'Debug_EnemyRanged' not in verify
assert verify.count('m_Name: DamagePopup') == 0
assert verify.count('fileID: %d}' % STRIPPED_TF) >= 2, 'stripped Transform 引用计数异常'
assert new_content.startswith('%YAML 1.1\n%TAG !u!')
print('[5] 全部断言通过：RunManager=1、fileID 引用=2、调试对象=0、DamagePopup=0')
print('手术成功')
