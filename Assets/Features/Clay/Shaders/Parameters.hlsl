#ifndef PARAMETERS_H
#define PARAMETERS_H

float radius;

float delta; // δ: 深度範囲（例: 粒子半径の10倍）
float mu; // µ: エッジカーブ強度（例: 粒子半径）
float sigma_world; // ワールド空間フィルタサイズ
int max_filter_radius; // カーネルピクセル半径
int direction; // 0=水平, 1=垂直, 2=2D(クリーンアップ)

float yaw_rad; // Yaw方向の回転

float shadow_step_size;
float shadow_bias;
float shadow_intensity;
int shadow_step_count;

float4 clay_color;

float4x4 object_to_world;

StructuredBuffer<float3> particle_pos;

#endif
