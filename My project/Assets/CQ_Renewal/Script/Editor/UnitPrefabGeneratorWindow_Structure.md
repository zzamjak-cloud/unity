# UnitPrefabGeneratorWindow 코드 구조 분석

## 개요
`UnitPrefabGeneratorWindow`는 Unity Editor에서 JSON 파일과 이미지 리소스를 기반으로 베리언트 프리팹을 자동 생성하는 도구입니다.

## 주요 기능
- JSON 파일을 기반으로 게임오브젝트 계층 구조 생성
- 이미지 리소스에 맞춰 동적 오브젝트 생성
- 스프라이트 자동 적용
- Animator 컨트롤러 자동 변경
- 베리언트 프리팹으로 저장

## 코드 구조 (Region 기반)

### 1. Constants and Configuration
```csharp
#region Constants and Configuration
```
**목적**: 전역 상수 및 설정값 관리

**포함 내용**:
- `FACE_EXPRESSION_NAMES`: Face 표정 오브젝트 이름들 (Normal, Happy, Attack, Blank)
- `HEAD_SORT_ORDER`: Head 오브젝트 정렬 순서
- `SUPPORTED_IMAGE_EXTENSIONS`: 지원하는 이미지 확장자 (.png, .jpg)
- `ANIMATOR_NAMES`: Animator 이름들 (Ar, Hu, Pa, Pr, Wa, Wi)

### 2. Helper Methods
```csharp
#region Helper Methods
```
**목적**: 공통으로 사용되는 유틸리티 메서드들

**포함 내용**:
- `IsFaceExpressionName(string name)`: Face 표정 이름 확인
- `IsSupportedImageFile(string filePath)`: 지원되는 이미지 파일 확인
- `IsValidAnimatorName(string name)`: 유효한 Animator 이름 확인

### 3. Fields
```csharp
#region Fields
```
**목적**: UI 필드 및 설정값들

**포함 내용**:
- `baseModelPrefab`: 기본 모델 프리팹
- `referenceJSON`: 참조용 JSON 파일
- `imageFolder`: 이미지 리소스 폴더
- `outputFolder`: 출력 폴더

### 4. Unity Editor Integration
```csharp
#region Unity Editor Integration
```
**목적**: Unity Editor와의 통합

**포함 내용**:
- `ShowWindow()`: Editor Window 표시

### 5. UI Methods
```csharp
#region UI Methods
```
**목적**: 사용자 인터페이스 관련 메서드들

**포함 내용**:
- `OnGUI()`: 메인 UI 렌더링
- `DrawHeader()`: 헤더 그리기
- `DrawInputFields()`: 입력 필드들 그리기
- `DrawGenerateButton()`: 생성 버튼 그리기

### 6. Main Logic
```csharp
#region Main Logic
```
**목적**: 메인 처리 로직

**포함 내용**:
- `ValidateInputs()`: 입력 필드 유효성 검사
- `SetupHierarchyFromJSON()`: JSON 기반 프리팹 생성 메인 로직

### 7. Prefab Management
```csharp
#region Prefab Management
```
**목적**: 프리팹 저장 및 관리

**포함 내용**:
- `SaveAsVariantPrefab()`: 베리언트 프리팹으로 저장

### 8. Sorting and Organization
```csharp
#region Sorting and Organization
```
**목적**: 오브젝트 정렬 및 조직화

**포함 내용**:
- `SortPivotChildrenAlphabetically()`: Pivot 하위 오브젝트 알파벳 정렬
- `SortHeadChildrenAlphabetically()`: Head 하위 오브젝트 정렬
- `CompareNamesWithNumbers()`: 숫자가 포함된 이름 비교

### 9. GameObject Creation and Management
```csharp
#region GameObject Creation and Management
```
**목적**: 게임오브젝트 생성 및 관리

**포함 내용**:
- `FindPivotTransform()`: Pivot Transform 찾기
- `FindAnimatorTransform()`: Animator Transform 찾기
- `ChangeAnimatorController()`: Animator 컨트롤러 변경
- `CreateGameObjectsFromJSON()`: JSON 기반 게임오브젝트 생성
- `CreateGameObjectFromData()`: 개별 게임오브젝트 생성
- `FindTransformByPath()`: 경로로 Transform 찾기
- `ApplyTransformData()`: Transform 데이터 적용
- `ApplyComponentData()`: 컴포넌트 데이터 적용

### 10. Dynamic Object Creation
```csharp
#region Dynamic Object Creation
```
**목적**: 동적 오브젝트 생성

**포함 내용**:
- `CreateDynamicObjectsFromSprites()`: 스프라이트 기반 동적 오브젝트 생성
- `FindTransformByName()`: 이름으로 Transform 찾기
- `CreateDynamicImageObjects()`: 이미지 오브젝트 생성
- `CreateDynamicVariantObjects()`: 변형 오브젝트 생성
- `CreateHeadObjects()`: Head 오브젝트 생성
- `CreateHeadImageObjects()`: Head 이미지 오브젝트 생성
- `CreateImageObject()`: 이미지 오브젝트 생성
- `HasSpriteInFolder()`: 폴더에 스프라이트 존재 확인
- `CreateDynamicVariantObjectsWithParentSearch()`: 상위 부모 검색으로 변형 오브젝트 생성

### 11. Sprite Management
```csharp
#region Sprite Management
```
**목적**: 스프라이트 관리

**포함 내용**:
- `ReplaceSprites()`: 스프라이트 교체
- `IsDynamicObject()`: 동적 오브젝트 확인

### 12. Sprite Name Processing
```csharp
#region Sprite Name Processing
```
**목적**: 스프라이트 이름 처리

**포함 내용**:
- `GetTargetSpriteName()`: 대상 스프라이트 이름 생성
- `IsFaceExpressionObject()`: Face 표정 오브젝트 확인
- `IsFaceExpressionImageObject()`: Face 표정 Image 오브젝트 확인
- `GetSpriteNamePrefix()`: 스프라이트 이름 접두사 추출

### 13. Sprite Grouping and File Processing
```csharp
#region Sprite Grouping and File Processing
```
**목적**: 스프라이트 그룹화 및 파일 처리

**포함 내용**:
- `GroupSpritesByVariantName()`: 베리언트 이름별 스프라이트 그룹화

### 14. Utility Methods
```csharp
#region Utility Methods
```
**목적**: 유틸리티 메서드들

**포함 내용**:
- `GetHierarchyPath()`: 계층 구조 경로 추출
- `CleanupEmptySpriteRenderers()`: 빈 Sprite Renderer 정리
- `CollectAllTransforms()`: 모든 Transform 수집

## 처리 흐름 (Main Logic)

### 1. 입력 검증
```csharp
ValidateInputs()
```
- 모든 필수 필드가 입력되었는지 확인

### 2. JSON 데이터 로드
```csharp
JsonUtility.FromJson<HierarchyData>(jsonContent)
```
- 참조 JSON 파일을 파싱하여 계층 구조 데이터 로드

### 3. 스프라이트 그룹화
```csharp
GroupSpritesByVariantName(imagePath)
```
- 이미지 폴더에서 스프라이트들을 베리언트별로 그룹화

### 4. 각 베리언트별 처리
```csharp
foreach (var entry in variantSprites)
```

#### 4.1. 기본 모델 로드
```csharp
PrefabUtility.InstantiatePrefab(baseModelPrefab)
```

#### 4.2. Animator 컨트롤러 변경
```csharp
ChangeAnimatorController(baseModelInstance, animatorName)
```
- 베리언트 이름의 첫 번째 부분(Ar, Hu, Pa, Pr, Wa, Wi)으로 Animator 컨트롤러 변경

#### 4.3. Pivot 오브젝트 찾기
```csharp
FindPivotTransform(baseModelInstance.transform)
```

#### 4.4. JSON 기반 게임오브젝트 생성
```csharp
CreateGameObjectsFromJSON(basePivot, hierarchyData)
```

#### 4.5. 동적 오브젝트 생성
```csharp
CreateDynamicObjectsFromSprites(baseModelInstance.transform, spritesForVariant, variantName)
```

#### 4.6. 스프라이트 적용
```csharp
ReplaceSprites(baseModelInstance.transform, spritesForVariant, variantName)
```

#### 4.7. 정렬 및 정리
```csharp
SortPivotChildrenAlphabetically(basePivot)
SortHeadChildrenAlphabetically(basePivot)
CleanupEmptySpriteRenderers(baseModelInstance.transform)
```

#### 4.8. 베리언트 프리팹 저장
```csharp
SaveAsVariantPrefab(baseModelInstance, variantName)
```

## 주요 데이터 구조

### HierarchyData
JSON 파일에서 로드되는 계층 구조 데이터

### GameObjectData
개별 게임오브젝트 정보
- Transform 데이터 (위치, 회전, 스케일)
- 컴포넌트 정보
- 부모-자식 관계

## 확장성

### 새로운 표정 추가
```csharp
private static readonly string[] FACE_EXPRESSION_NAMES = 
{
    "Normal", "Happy", "Attack", "Blank",
    "NewExpression"  // 새로 추가
};
```

### 새로운 Animator 추가
```csharp
private static readonly string[] ANIMATOR_NAMES = 
{
    "Ar", "Hu", "Pa", "Pr", "Wa", "Wi",
    "NewAnimator"  // 새로 추가
};
```

### 새로운 이미지 확장자 지원
```csharp
private static readonly string[] SUPPORTED_IMAGE_EXTENSIONS = 
{ 
    ".png", ".jpg", ".jpeg"  // 새로 추가
};
```

## 사용 예시

### 입력
- **Base Model Prefab**: PlayerModelBase.prefab
- **Reference JSON**: hierarchy_data.json
- **Image Folder**: Assets/Images/Characters/
- **Output Folder**: Assets/GeneratedPrefabs/

### 출력
- **Wi_Dorothy_6.prefab**: Wi Animator Controller 적용
- **Pa_Roland_6.prefab**: Pa Animator Controller 적용
- **Wa_Leon_4.prefab**: Wa Animator Controller 적용

## 주의사항

1. **Animator Controller 파일**: `Assets/Animations/{AnimatorName}.controller` 경로에 존재해야 함
2. **이미지 파일명**: `{VariantName}_{ObjectName}` 형식이어야 함
3. **JSON 구조**: 올바른 계층 구조 데이터가 포함되어야 함
4. **프리팹 구조**: 기본 모델에 Pivot/Animator 구조가 있어야 함
