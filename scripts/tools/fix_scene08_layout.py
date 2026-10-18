#!/usr/bin/env python3
"""
修复 scene_08 的 layout.json，基于词典规范：
- water 层：scale.x=200，不滚动
- Mid_color：渐变条宽度调整
- tree：调整到 Mid_A_all 循环范围内
- cattle_shrub_4：调整到 Mid_B_1 循环范围内
"""
import json
import os
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import _paths

def fix_scene08():
    layout_path = Path(_paths.repo_root()) / "PlayClient" / "Unity" / "Assets" / "StreamingAssets" / "MdLooks" / "sprites" / "scene_08" / "bg" / "layout.json"
    
    print(f"读取: {layout_path}")
    with open(layout_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    groups = data['groups']
    items = data['items']
    sprites = data.get('sprites', [])
    
    # 根节点位置（词典规范）
    root_x, root_y = -22.3, -3.66
    
    changes = []
    
    # 1. 修复 water 层：scale.x=200，不滚动
    water_group = None
    for g in groups:
        if g['name'] == 'water':
            water_group = g
            break
    
    if water_group:
        old_x = water_group['x']
        # water 层位置应该让 200 单位宽的纹理覆盖屏幕
        # 屏幕中心 x=0，需要覆盖 [-8, 8]
        # water 纹理中心应该在根节点 x=-22.3 附近
        water_group['x'] = root_x
        water_group['loop'] = 0.0
        water_group['period'] = 0.0
        water_group['loopW'] = 0.0
        water_group['periodS'] = 0.0
        changes.append(f"water 层: x {old_x:.3f} → {water_group['x']:.3f}, loop=0")
        
        # 修复 water 物品
        water_gi = groups.index(water_group)
        for it in items:
            if it['gi'] == water_gi:
                old_it_x = it['x']
                it['x'] = water_group['x']
                it['y'] = water_group['y']
                changes.append(f"  water 物品: x {old_it_x:.3f} → {it['x']:.3f}")
    
    # 2. 修复 Mid_color 层：渐变条
    mid_color_group = None
    for g in groups:
        if g['name'] == 'Mid_color':
            mid_color_group = g
            break
    
    if mid_color_group:
        mid_color_gi = groups.index(mid_color_group)
        for it in items:
            if it['gi'] == mid_color_gi and it['spr'] == 'gradual_change':
                old_w = it['w']
                # 渐变条应该覆盖整个屏幕宽度，大约 20-30 单位
                it['w'] = 30.0
                it['x'] = mid_color_group['x']
                changes.append(f"gradual_change: w {old_w:.3f} → {it['w']:.3f}, x → {it['x']:.3f}")
    
    # 3. 修复 Mid_A_all 层的 tree
    mid_a_all_group = None
    for g in groups:
        if g['name'] == 'Mid_A_all':
            mid_a_all_group = g
            break
    
    if mid_a_all_group:
        mid_a_all_gi = groups.index(mid_a_all_group)
        mid_a_all_x = mid_a_all_group['x']
        mid_a_all_loop = mid_a_all_group['loop']
        
        # 词典规范：tree x≈21（世界坐标）
        # Mid_A_all 层位置 x=-55.69, loop=63.5
        # 树应该在层循环范围内：[-55.69, -55.69+63.5] = [-55.69, 7.81]
        # 但词典说 tree x≈21，这是世界坐标
        # 需要转换到层内本地坐标：21 - (-55.69) = 76.69
        # 这超出了循环范围，说明树应该在循环的某个副本中
        
        tree_world_x = 21.0  # 词典规范
        tree_local_x = tree_world_x - mid_a_all_x  # 76.69
        
        # 调整到循环范围内：76.69 - 63.5 = 13.19（第一个副本）
        # 或者 76.69 - 2*63.5 = -50.31（上一个副本）
        # 选择最接近层位置的
        if tree_local_x > mid_a_all_loop:
            tree_local_x -= mid_a_all_loop
            changes.append(f"tree 本地坐标调整: 76.69 → {tree_local_x:.3f} (减去一个循环)")
        
        for it in items:
            if it['gi'] == mid_a_all_gi and it['spr'] == 'tree':
                old_x = it['x']
                it['x'] = mid_a_all_x + tree_local_x
                changes.append(f"  tree: x {old_x:.3f} → {it['x']:.3f}")
    
    # 4. 修复 Mid_B_1 层的 cattle_shrub_4
    mid_b_1_group = None
    for g in groups:
        if g['name'] == 'Mid_B_1':
            mid_b_1_group = g
            break
    
    if mid_b_1_group:
        mid_b_1_gi = groups.index(mid_b_1_group)
        mid_b_1_x = mid_b_1_group['x']
        mid_b_1_loop = mid_b_1_group['loop']
        
        for it in items:
            if it['gi'] == mid_b_1_gi and it['spr'] == 'cattle_shrub_4':
                old_x = it['x']
                # 调整到循环范围内
                local_x = old_x - mid_b_1_x
                if local_x > mid_b_1_loop:
                    local_x -= mid_b_1_loop
                    it['x'] = mid_b_1_x + local_x
                    changes.append(f"  cattle_shrub_4: x {old_x:.3f} → {it['x']:.3f}")
    
    # 5. 检查并修复 cattle（钟楼）位置
    # 词典规范：钟楼 x≈2.12
    mid_b_all_group = None
    for g in groups:
        if g['name'] == 'Mid_B_all':
            mid_b_all_group = g
            break
    
    if mid_b_all_group:
        mid_b_all_gi = groups.index(mid_b_all_group)
        cattle_world_x = 2.12
        cattle_local_x = cattle_world_x - mid_b_all_group['x']  # 2.12 - (-54.3) = 56.42
        
        for it in items:
            if it['gi'] == mid_b_all_gi and it['spr'] == 'cattle':
                old_x = it['x']
                it['x'] = mid_b_all_group['x'] + cattle_local_x
                changes.append(f"  cattle: x {old_x:.3f} → {it['x']:.3f}")
    
    # 6. 检查并修复石拱位置
    # 词典规范：石拱约 −3.17 / −1.94
    stone_arch_world_xs = [-3.17, -1.94]
    if mid_b_all_group:
        for arch_world_x in stone_arch_world_xs:
            arch_local_x = arch_world_x - mid_b_all_group['x']
            for it in items:
                if it['gi'] == mid_b_all_gi and 'stone' in it['spr'].lower():
                    old_x = it['x']
                    # 只调整接近的物品
                    if abs(old_x - (mid_b_all_group['x'] + arch_local_x)) < 5:
                        it['x'] = mid_b_all_group['x'] + arch_local_x
                        changes.append(f"  {it['spr']}: x {old_x:.3f} → {it['x']:.3f}")
    
    # 写回文件
    print(f"\n应用 {len(changes)} 项修改:")
    for change in changes:
        print(f"  {change}")
    
    with open(layout_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    
    print(f"\n已写入: {layout_path}")
    print("\n建议在 Unity 中重新编译并测试")

if __name__ == '__main__':
    fix_scene08()
