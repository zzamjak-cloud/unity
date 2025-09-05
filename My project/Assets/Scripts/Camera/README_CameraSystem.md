# Follow Camera 시스템 사용 가이드

## 개요
이 Follow Camera 시스템은 플레이어 캐릭터를 자연스럽게 따라다니며, 캐릭터의 이동 방향에 따라 전방 시야를 확보하는 Lazy Follow Camera입니다.

## 주요 기능

### 1. Lazy Follow
- 캐릭터가 일정 거리 이상 멀어져야 카메라가 따라감
- 자연스러운 카메라 움직임으로 몰입감 향상
- 설정 가능한 Lazy Distance와 Lazy Speed

### 2. Look Ahead (전방 시야 확보)
- 캐릭터의 이동 방향으로 카메라가 미리 이동
- 달리기 시 더 넓은 전방 시야 확보
- Player의 입력과 Rigidbody 속도를 기반으로 방향 계산

### 3. 부드러운 이동
- 카메라 이동 시 부드러운 보간 적용
- 설정 가능한 Smoothing Factor

### 4. 경계 제한
- 카메라가 특정 영역을 벗어나지 않도록 제한
- Min/Max Boundary 설정 가능

### 5. 성능 최적화
- 업데이트 빈도 조절 (60fps/30fps/20fps)
- 전방 시야 캐싱으로 CPU 사용량 감소
- 메모리 할당 최소화

### 6. 추가 효과
- 화면 흔들림 효과 (Screen Shake)
- 줌 인/아웃 효과
- 부드러운 카메라 전환

## 설치 및 설정

### 1. 기본 설정
1. Main Camera에 `FollowCameraOptimized` 스크립트 추가
2. `CameraManager` 스크립트를 별도 GameObject에 추가
3. Player GameObject에 "Player" 태그 설정

### 2. FollowCameraOptimized 설정
```csharp
// Inspector에서 설정 가능한 주요 값들
[Header("Target Settings")]
- Target: 따라갈 대상 (Player)
- Auto Find Player: 자동으로 Player 찾기

[Header("Follow Settings")]
- Offset: 카메라 오프셋 (기본: 0, 2, -10)
- Follow Speed: 따라가는 속도 (기본: 5)
- Rotation Speed: 회전 속도 (기본: 2)

[Header("Lazy Follow Settings")]
- Lazy Distance: Lazy follow 거리 (기본: 2)
- Lazy Speed: Lazy follow 속도 (기본: 3)

[Header("Look Ahead Settings")]
- Enable Look Ahead: 전방 시야 확보 활성화
- Look Ahead Distance: 전방 시야 거리 (기본: 3)
- Look Ahead Speed: 전방 시야 이동 속도 (기본: 2)
- Look Ahead Multiplier: 달리기 시 전방 시야 배수 (기본: 1.5)

[Header("Boundary Settings")]
- Enable Boundaries: 경계 제한 활성화
- Min/Max Boundary: 경계 범위

[Header("Advanced Smoothing")]
- Use Advanced Smoothing: 고급 스무딩 사용
- Position Smoothing: 위치 스무딩 (기본: 0.06)
- Velocity Smoothing: 속도 스무딩 (기본: 0.05)
- Max Velocity: 최대 속도 제한 (기본: 5)

[Header("Performance Optimization")]
- Enable Performance Mode: 성능 모드 활성화
- Update Frequency: 업데이트 빈도 (1=60fps, 2=30fps, 3=20fps)
- Look Ahead Update Interval: 전방 시야 업데이트 간격 (기본: 0.1초)
```

### 3. CameraManager 설정
```csharp
[Header("Screen Shake Settings")]
- Shake Intensity: 흔들림 강도 (기본: 0.5)
- Shake Duration: 흔들림 지속 시간 (기본: 0.3)

[Header("Zoom Settings")]
- Default Orthographic Size: 기본 직교 크기 (기본: 5)
- Zoom Speed: 줌 속도 (기본: 2)
- Min/Max Zoom: 줌 범위 (기본: 3~8)
```

## 사용법

### 1. 기본 사용
```csharp
// FollowCameraOptimized는 자동으로 Player를 찾아서 따라다님
// 별도의 활성화/비활성화 코드가 필요하지 않음
```

### 2. 화면 흔들림 효과
```csharp
// 기본 흔들림
CameraManager.ShakeScreen();

// 커스텀 흔들림
CameraManager.ShakeScreen(1.0f, 0.5f); // 강도 1.0, 지속시간 0.5초
```

### 3. 줌 효과
```csharp
// 줌 설정
CameraManager.SetCameraZoom(3.0f); // 3.0 크기로 줌

// 부드러운 줌
CameraManager.SetCameraZoom(7.0f, true);

// 기본 줌으로 복원
CameraManager.Instance.ResetZoom();
```

### 4. 카메라 이동
```csharp
// 특정 위치로 이동
CameraManager.Instance.MoveTo(new Vector3(10, 5, -10));

// 부드러운 이동
CameraManager.Instance.MoveTo(targetPosition, true);
```

### 5. 성능 최적화 설정
```csharp
// FollowCameraOptimized 성능 설정
FollowCameraOptimized optimizedCamera = GetComponent<FollowCameraOptimized>();

// 성능 모드 설정 (30fps, 0.15초 간격)
optimizedCamera.SetPerformanceMode(true, 2, 0.15f);

// 고급 스무딩 설정
optimizedCamera.SetAdvancedSmoothing(true, 0.06f, 0.05f, 5f);
```

### 6. 설정 적용
```csharp
// CameraSettings 사용
CameraSettings settings = Resources.Load<CameraSettings>("CameraSettings");
settings.ApplyToFollowCamera(optimizedCamera);
settings.ApplyToCameraManager(cameraManager);
```

## 디버그 기능

### 1. 디버그 정보 표시
- Inspector에서 "Show Debug Info" 체크
- 게임 실행 시 화면 좌상단에 디버그 정보 표시

### 2. 기즈모 표시
- Inspector에서 "Show Gizmos" 체크
- Scene 뷰에서 카메라 관련 정보 시각화
- 빨간색: 타겟 위치
- 파란색: 카메라 위치
- 노란색: 전방 시야 위치
- 초록색: 경계 영역
- 청록색: Lazy follow 거리

## 최적화 팁

### 1. 성능 최적화
- `enableSmoothing`을 false로 설정하여 부드러운 이동 비활성화
- `enableLookAhead`를 false로 설정하여 전방 시야 계산 비활성화
- 불필요한 디버그 기능 비활성화

### 2. 설정 최적화
- 게임 타입에 맞는 적절한 `lazyDistance` 설정
- 플레이어 이동 속도에 맞는 `followSpeed` 조정
- 맵 크기에 맞는 `boundary` 설정

## 문제 해결

### 1. 카메라가 Player를 따라가지 않는 경우
- Player GameObject에 "Player" 태그가 설정되어 있는지 확인
- `autoFindPlayer`가 true로 설정되어 있는지 확인
- Target이 올바르게 설정되어 있는지 확인
- `FollowCameraOptimized` 컴포넌트가 Main Camera에 추가되어 있는지 확인

### 2. 카메라가 너무 빠르게 움직이는 경우
- `followSpeed` 값을 낮춤
- `positionSmoothing` 값을 낮춤
- `useAdvancedSmoothing`을 true로 설정
- `maxVelocity` 값을 낮춤

### 3. 전방 시야가 작동하지 않는 경우
- `enableLookAhead`가 true로 설정되어 있는지 확인
- Player에 `PlayerController` 컴포넌트가 있는지 확인
- `lookAheadDistance` 값이 적절한지 확인
- `lookAheadUpdateInterval` 값이 너무 크지 않은지 확인

### 4. 성능 문제가 있는 경우
- `enablePerformanceMode`를 true로 설정
- `updateFrequency`를 2 또는 3으로 설정 (30fps 또는 20fps)
- `lookAheadUpdateInterval`을 0.2f 이상으로 설정
- 저성능 기기에서는 `enableLookAhead`를 false로 설정

## 확장 가능성

### 1. 추가 기능
- 카메라 경로 시스템
- 시네마틱 카메라 전환
- 다중 타겟 추적
- 카메라 필터 효과

### 2. 커스터마이징
- 새로운 카메라 효과 추가
- 커스텀 애니메이션 커브 사용
- 게임 상태에 따른 동적 설정 변경

## 최종 카메라 시스템 구성

### 핵심 파일들
- `FollowCameraOptimized.cs` - 메인 카메라 시스템
- `CameraManager.cs` - 카메라 효과 관리
- `CameraSettings.cs` - 설정 관리
- `README_CameraSystem.md` - 사용 가이드

### 특징
- 자연스러운 Lazy Follow 움직임
- 성능 최적화 (업데이트 빈도 조절)
- 전방 시야 확보
- 화면 흔들림, 줌 등 추가 효과
- 모바일 최적화

이 시스템을 통해 자연스럽고 몰입감 있는 카메라 경험을 제공할 수 있습니다.
