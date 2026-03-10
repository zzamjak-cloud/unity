# FlexibleLineRenderer

LineRenderer를 활용하여 가상 물리 효과(Verlet 통합 기반 로프/케이블 시뮬레이션)를 구현하는 컴포넌트입니다.

## 개요

두 앵커 포인트 사이에 LineRenderer를 배치하고, Verlet 물리 시뮬레이션으로 중력·댐핑·강성(stiffness)을 적용하여 자연스러운 로프/줄 효과를 표현합니다.

## 사용 방법

1. GameObject에 `FlexibleLineRenderer` 컴포넌트 추가
2. **Start Anchor** / **End Anchor**에 시작·끝 Transform 할당
3. LineRenderer가 없으면 자동 추가됨

## 주요 설정

### 필수 컴포넌트
| 필드 | 설명 |
|------|------|
| Line Renderer | 라인을 그리는 LineRenderer (미할당 시 자동 추가) |
| Start Anchor | 로프 시작점 Transform |
| End Anchor | 로프 끝점 Transform |

### 라인 설정
| 필드 | 설명 | 기본값 |
|------|------|--------|
| Segment Count | 라인을 구성하는 세그먼트(노드) 수 | 20 |
| Segment Length | 세그먼트 간 거리 (수동 설정 시) | 0.2 |
| Auto Calculate Segment Length | 앵커 간 거리로 세그먼트 길이 자동 계산 | true |

### 시뮬레이션 설정
| 필드 | 설명 | 기본값 |
|------|------|--------|
| **Simulate In Editor** | **체크 해제 시 에디터에서 시뮬레이션하지 않고 런타임에서만 동작** | true |
| Constraint Iterations | 거리 제약 반복 횟수 (높을수록 정확하지만 비용 증가) | 10 |
| Gravity Resistance | 중력 영향 계수 | 1.5 |
| Damping | 속도 감쇠 (0~1, 1에 가까울수록 느리게 멈춤) | 0.95 |
| Stiffness | 강성 (세그먼트 간 거리 보정 강도) | 0.3 |
| Use Gravity | 중력 사용 여부 | true |
| Custom Gravity | 커스텀 중력 벡터 | (0, -9.81, 0) |

### 성능 최적화
| 필드 | 설명 | 기본값 |
|------|------|--------|
| Enable Performance Optimization | 캐싱 및 업데이트 레이트 제어 활성화 | true |
| Update Rate | 초당 시뮬레이션 업데이트 횟수 | 60 |
| Skip Update When Invisible | 화면에 보이지 않을 때 업데이트 스킵 | true |

### 에디터 표시
| 필드 | 설명 | 기본값 |
|------|------|--------|
| Show In Editor | 에디터에서 라인 표시 여부 | true |
| Update In Editor | 에디터에서 업데이트 실행 여부 | true |
| Editor Line Color | Gizmo 라인 색상 | Cyan |
| Show Editor Gizmos | Gizmo 표시 여부 | true |

## 물리 알고리즘

**Verlet Integration** 기반:
1. 각 세그먼트 노드의 속도를 현재 위치 - 이전 위치로 계산
2. 속도에 댐핑 적용 후 중력 가속
3. 거리 제약(Distance Constraint)을 반복 적용하여 세그먼트 길이 유지
4. 양 끝 앵커는 매 반복마다 고정 위치로 리셋

## 공개 메서드

| 메서드 | 설명 |
|--------|------|
| `SetAnchors(Transform start, Transform end)` | 런타임에서 앵커 변경 |
| `RefreshLineRenderer()` | LineRenderer 설정 재적용 |

## Context Menu (에디터)

- **에디터에서 초기화**: 수동으로 에디터 시뮬레이션 초기화
- **위치 리셋**: 모든 노드를 앵커 사이 직선으로 리셋
