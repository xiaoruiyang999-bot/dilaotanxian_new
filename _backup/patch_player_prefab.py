# -*- coding: utf-8 -*-
"""Player.prefab 手术：把 WerewolfVisual（GO/TF/SR/FA）植入 prefab + 新增 WerewolfTransformation 组件。
让所有 Player 实例自动继承（替代会被 Unity 丢弃的 prefab-instance added-component）。"""
import re

PREFAB = 'Assets/Prefabs/Player.prefab'
WT_GUID = '95bd01fa6081e52428215d3d5e70e78c'
FA_GUID = '88cc77660a5520440ad6be968e98c12e'
ROOT_GO = '5645691151929306351'
ROOT_TF = '857263768900391870'
N_WT, N_GO, N_TF, N_SR, N_FA = ('7770000000000000003', '7770000000000000004',
                                '7770000000000000005', '7770000000000000006',
                                '7770000000000000007')

parts = eval(open('_backup/werewolf_blocks.json', encoding='utf-8').read())

def clean(block, old_fid, new_fid, go_target):
    b = block.replace('&' + old_fid, '&' + new_fid)
    b = re.sub(r'm_GameObject: \{fileID: \d+\}', 'm_GameObject: {fileID: ' + go_target + '}', b)
    b = re.sub(r'm_CorrespondingSourceObject: \{fileID: [^}]*\}',
               'm_CorrespondingSourceObject: {fileID: 0}', b)
    b = re.sub(r'm_PrefabInstance: \{fileID: [^}]*\}', 'm_PrefabInstance: {fileID: 0}', b)
    return b

go = clean(parts['GO'], '12156515', N_GO, '0')
go = re.sub(r'component: \{fileID: 12156516\}', 'component: {fileID: ' + N_TF + '}', go)
go = re.sub(r'component: \{fileID: 12156517\}', 'component: {fileID: ' + N_SR + '}', go)
go = re.sub(r'component: \{fileID: 12156518\}', 'component: {fileID: ' + N_FA + '}', go)

tf = clean(parts['TF'], '12156516', N_TF, N_GO)
tf = re.sub(r'm_Father: \{fileID: [^}]*\}', 'm_Father: {fileID: ' + ROOT_TF + '}', tf)
tf = re.sub(r'- \{fileID: \d+\}', '', tf)  # 场景提取块不该有子物体引用

sr = clean(parts['SR'], '12156517', N_SR, N_GO)
fa = clean(parts['FA'], '12156518', N_FA, N_GO)

wt = f"""--- !u!114 &{N_WT}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {ROOT_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {WT_GUID}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  werewolfVisual: {{fileID: {N_TF}}}
  transformFolder: Assets/Art/Werewolf/Transform_L
  walkLFolder: Assets/Art/Werewolf/Walk_L
  walkRFolder: Assets/Art/Werewolf/Walk_R
  transformFps: 8
  walkFps: 10
"""

content = open(PREFAB, encoding='utf-8', newline='').read()
assert WT_GUID not in content, 'prefab 中已存在 WT 组件，拒绝重复插入'
# 根 GO 组件列表加 WT
content = re.sub(r'(m_Component:\n(?:  - component: \{fileID: \d+\}\n)+)',
                 r'\1  - component: {fileID: ' + N_WT + '}\n', content, count=1)
# 块追加到文件尾
if not content.endswith('\n'):
    content += '\n'
content += wt + go + tf + sr + fa
open(PREFAB, 'w', encoding='utf-8', newline='').write(content)
print('prefab 手术完成：WT + WerewolfVisual(GO/TF/SR/FA) 已植入')
