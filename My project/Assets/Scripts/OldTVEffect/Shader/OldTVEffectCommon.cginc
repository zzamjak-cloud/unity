#ifndef CAT_OLDTV_COMMON_INCLUDED
#define CAT_OLDTV_COMMON_INCLUDED

// OldTVEffect Sprite/UI 셰이더 공용 로직
// 정밀도 규칙: 시간·UV·주사선 좌표는 float, 색상만 half (모바일 16bit half 정밀도 붕괴 방지)

sampler2D _NoiseTex;
half _NoiseIntensity;
float _NoiseScale;
half _ScanLineIntensity;
float _ScanLineCount;
float _ScanLineThicknessInv;
float _VerticalJitter;
float _HorizontalJitter;
half _ColorBleed;
float _ColorBleedOffset;
float _RollSpeed;
half _Saturation;
float4 _UVRect;

// 에디터 60초 테스트용 전역 시간 오프셋 (Shader.SetGlobalFloat, 플레이 모드에서는 0)
float _CAT_TVEditorTime;

float TV_Time()
{
    return _Time.y + _CAT_TVEditorTime;
}

// sin 기반 해시는 일부 모바일 GPU 의 저정밀 sin 구현에서 상관/고착 → 곱셈 기반 해시 사용
float TV_Hash(float n)
{
    n = frac(n * 0.1031);
    n *= n + 33.33;
    n *= n + n;
    return frac(n);
}

// 버텍스 단계: 프레임 단위(초당 24회) 랜덤 지터를 UV 오프셋으로, 노이즈 UV 는 매 갱신마다 재배치
// → 프래그먼트에서 노이즈 샘플 결과로 메인 UV 를 결정하는 종속 텍스처 읽기가 사라짐
// 지터 시계는 _RollSpeed 와 독립 (롤링 속도 0 이어도 떨림·노이즈는 계속 갱신)
void TV_ComputeUVs(float2 texcoord, out float2 jitteredUV, out float2 noiseUV)
{
    // fp32 정밀도 유지를 위해 주기적으로 감음 (170초 주기, 체감 불가)
    float n = fmod(floor(TV_Time() * 24.0), 4096.0);

    float2 jitter = float2(TV_Hash(n), TV_Hash(n + 17.0)) * 2.0 - 1.0;
    jitteredUV = texcoord + jitter * float2(_HorizontalJitter, _VerticalJitter);

    noiseUV = texcoord * _NoiseScale + float2(TV_Hash(n + 31.0), TV_Hash(n + 57.0));
}

// 아틀라스 이웃 스프라이트가 비치지 않도록 스프라이트 UV 범위로 클램프
float2 TV_ClampUV(float2 uv)
{
    return clamp(uv, _UVRect.xy, _UVRect.zw);
}

half3 TV_ApplyColorBleed(half3 col, half bleedR, half bleedB)
{
    col.r = lerp(col.r, bleedR, _ColorBleed);
    col.b = lerp(col.b, bleedB, _ColorBleed * 0.5);
    return col;
}

// 채도 → 그레인 → 주사선 순으로 적용. texV: 텍스처 V, screenPos: SV_POSITION (픽셀 좌표)
half3 TV_ApplyPostEffects(half3 col, float2 noiseUV, float texV, float4 screenPos)
{
    half lum = dot(col, half3(0.299, 0.587, 0.114));
    col = lerp(half3(lum, lum, lum), col, _Saturation);

    // 노이즈 텍스처는 Linear 로 샘플되어야 (noise - 0.5) 가 대칭 그레인이 됨 (C# 기본 노이즈는 linear 생성)
    half noise = tex2D(_NoiseTex, noiseUV).r;
    col += (noise - 0.5) * _NoiseIntensity * 0.4;

    #ifdef _SCREENSPACE_SCANLINES
    // API 별 원점 차이 정규화: 항상 0 = 화면 아래
    float lineCoord = screenPos.y / _ScreenParams.y;
    #if UNITY_UV_STARTS_AT_TOP
    lineCoord = 1.0 - lineCoord;
    #endif
    #else
    float lineCoord = texV;
    #endif

    // TV_Time 은 씬 로드 기준 _Time.y 라 통상 세션 길이에서는 fp32 로 충분 (수 시간 연속 실행 시 위상 양자화 가능)
    float roll = frac(lineCoord + TV_Time() * _RollSpeed * 0.1);
    // scan ∈ [0.5, 1] : 주사선 최대 감쇠는 _ScanLineIntensity * 0.5 (원본 룩 유지)
    float scan = sin(frac(roll * _ScanLineCount) * 3.14159265) * 0.5 + 0.5;
    scan = pow(scan, _ScanLineThicknessInv);
    col *= 1.0 - _ScanLineIntensity * (1.0 - (half)scan);

    return saturate(col);
}

#endif
