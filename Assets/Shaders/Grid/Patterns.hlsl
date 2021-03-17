#ifndef PATTERNS_INCLUDED
#define PATTERNS_INCLUDED

static const float SMOOTH_EDGE = 0.01;
static const float M_SQRT_2 = 0.707106781186547524401; // 1 / sqrt(2)
static const float SQRT_2 = 1.4142135623730951;

inline float stepAA(float value) {
  // float2 pd2 = float2(ddx(value), ddy(value));
	// float pd = sqrt( dot( pd2, pd2 ) );
  float pd = fwidth(value);

  // sooooooooooooo this is complicated, whether it should be +0 or +.5
  return saturate(value / max(0.00001, pd) + 0.5);
}

inline float stepAA(float thresh, float value){
	return stepAA(value - thresh);
}

inline float ptn_squre(float2 uv, float3 wpos, float size) {
  float2 st = frac(uv) * 2 - 1;
  float2 grid = abs(st) / fwidth(st);
  float l = 1.0 - min(grid.x, grid.y);

  return smoothstep(1 - size, 1, l);
}

inline float ptn_dot(float2 uv, float3 wpos, float size) {
  float2 st = frac(uv);
  float sdf = distance(st, float2(0.5, 0.5)) - size;

  return smoothstep(0.1, 0.099, sdf);
}

#endif
