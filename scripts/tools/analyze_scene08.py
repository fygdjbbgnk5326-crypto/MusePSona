#!/usr/bin/env python3
"""分析 scene_08 的 layout.json，检查坐标和物品分布"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import _paths

def analyze_scene08():
    layout_path = os.path.join(
        _paths.repo_root(), "PlayClient", "Unity", "Assets", "StreamingAssets",
        "MdLooks", "sprites", "scene_08", "bg", "layout.json")
    
    with open(layout_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    groups = data['groups']
    items = data['items']
    sprites = data.get('sprites', [])
    
    print("=" * 80)
    print("Scene 08 Layout Analysis")
    print("=" * 80)
    
    # 根节点位置
    root_x, root_y = -22.3, -3.66
    print(f"\n根节点位置: ({root_x}, {root_y})")
    print()
    
    # 按层统计
    for gi, group in enumerate(groups):
        name = group['name']
        gx = group['x']
        gy = group['y']
        loop = group['loop']
        period = group['period']
        mode = group.get('mode', 'scroll')
        
        # 找该层的所有物品
        layer_items = [it for it in items if it.get('gi') == gi]
        
        print(f"[gi={gi}] {name}")
        print(f"  层位置: ({gx:.3f}, {gy:.3f})")
        print(f"  相对根节点: ({gx - root_x:.3f}, {gy - root_y:.3f})")
        print(f"  循环: loop={loop:.3f}, period={period:.3f}s, mode={mode}")
        print(f"  物品数: {len(layer_items)}")
        
        if layer_items:
            xs = [it['x'] for it in layer_items]
            ys = [it['y'] for it in layer_items]
            print(f"  X 范围: [{min(xs):.3f}, {max(xs):.3f}]")
            print(f"  Y 范围: [{min(ys):.3f}, {max(ys):.3f}]")
            
            # 检查物品是否在层位置附近
            if loop > 0.1:
                # 有滚动
                x_min_expected = gx - loop
                x_max_expected = gx + loop
                out_of_range = [it for it in layer_items 
                               if it['x'] < x_min_expected - 1 or it['x'] > x_max_expected + 1]
                if out_of_range:
                    print(f"  ⚠️  {len(out_of_range)} 个物品超出滚动范围")
                    for it in out_of_range[:3]:
                        print(f"     - {it['spr']}: x={it['x']:.3f}")
            else:
                # 无滚动，检查物品是否靠近层位置
                far_items = [it for it in layer_items if abs(it['x'] - gx) > 50]
                if far_items:
                    print(f"  ⚠️  {len(far_items)} 个物品远离层位置")
                    for it in far_items[:3]:
                        print(f"     - {it['spr']}: x={it['x']:.3f}, dx={it['x'] - gx:.3f}")
        
        print()
    
    # 检查关键物品
    print("=" * 80)
    print("关键物品检查")
    print("=" * 80)
    
    key_spr_names = ['cattle', 'cattle_gate', 'tree', 'moon', 'ice_1', 'floor_1', 'floor_2']
    
    for spr_name in key_spr_names:
        found_items = [it for it in items if it['spr'] == spr_name]
        if found_items:
            print(f"\n{spr_name}: {len(found_items)} 个实例")
            for i, it in enumerate(found_items[:5]):
                gi = it['gi']
                group = groups[gi]
                print(f"  [{i}] gi={gi} ({group['name']}), x={it['x']:.3f}, y={it['y']:.3f}")
                print(f"      相对层: dx={it['x'] - group['x']:.3f}, dy={it['y'] - group['y']:.3f}")
        else:
            print(f"\n{spr_name}: ❌ 未找到")
    
    # 检查是否有物品分配到了错误的层
    print("\n" + "=" * 80)
    print("可能的层分配错误")
    print("=" * 80)
    
    for it in items:
        gi = it['gi']
        group = groups[gi]
        gx = group['x']
        loop = group['loop']
        
        # 如果物品 x 坐标远离层位置，且该层有滚动
        if loop > 0.1:
            # 物品应该在 [gx - loop, gx + loop] 范围内
            if it['x'] < gx - loop - 5 or it['x'] > gx + loop + 5:
                print(f"⚠️  {it['spr']} (gi={gi} {group['name']}): x={it['x']:.3f}")
                print(f"   层范围: [{gx - loop:.3f}, {gx + loop:.3f}]")
                print(f"   偏移: {it['x'] - gx:.3f}")
                print()

if __name__ == '__main__':
    analyze_scene08()
