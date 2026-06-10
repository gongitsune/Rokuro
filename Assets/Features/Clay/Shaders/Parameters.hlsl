#ifndef PARAMETERS_H
#define PARAMETERS_H

float radius;

float delta; // δ: 深度範囲（例: 粒子半径の10倍）
float mu; // µ: エッジカーブ強度（例: 粒子半径）
float sigma_world; // ワールド空間フィルタサイズ
int max_filter_radius; // カーネルピクセル半径
int direction; // 0=水平, 1=垂直, 2=2D(クリーンアップ)

float4 clay_color;

StructuredBuffer<float3> particle_pos;
Texture2D clay_main_tex, clay_normal_tex;

#endif
