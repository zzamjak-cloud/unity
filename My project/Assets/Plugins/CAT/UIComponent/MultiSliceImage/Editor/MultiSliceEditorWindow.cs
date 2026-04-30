#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using CAT.UI;

/// <summary>
/// MultiSliceImage 컴포넌트의 에디터 윈도우
/// </summary>

public class MultiSliceEditorWindow : EditorWindow
{
    private MultiSliceImage targetImage;
    public MultiSliceImage TargetImage => targetImage; // Inspector에서 접근용
    private Sprite currentSprite;
    private Texture2D cachedTexture;
    private Vector4 cachedUV;
    
    private Rect imageRect;
    private int draggingLineIndex = -1;
    private bool isDraggingVertical = false;

    // 스타일
    private GUIStyle headerStyle;

    public static void Open(MultiSliceImage target)
    {
        MultiSliceEditorWindow win = GetWindow<MultiSliceEditorWindow>("Slice Editor");
        win.SetTarget(target);
        win.minSize = new Vector2(400, 300);
        win.Show();
    }

    public void SetTarget(MultiSliceImage target)
    {
        targetImage = target;
        // 타겟이 변경되면 캐시 초기화
        currentSprite = null;
        cachedTexture = null;
        cachedUV = Vector4.zero;
    }

    private void OnEnable()
    {
        // 프로젝트 변경(아틀라스 리패킹 등) 감지 시 리페인트
        EditorApplication.projectChanged += OnProjectChanged;
        Undo.undoRedoPerformed += Repaint;
        Selection.selectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
        Undo.undoRedoPerformed -= Repaint;
        Selection.selectionChanged -= OnSelectionChanged;
    }
    
    private void OnSelectionChanged()
    {
        // targetImage가 null이거나 선택된 오브젝트가 변경된 경우 업데이트
        if (targetImage == null || targetImage.Equals(null))
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                MultiSliceImage image = selected.GetComponent<MultiSliceImage>();
                if (image != null)
                {
                    SetTarget(image);
                    Repaint();
                }
            }
        }
    }

    private void OnProjectChanged()
    {
        // 아틀라스가 변경되면 캐시를 초기화하고 강제로 다시 그려서 이미지를 갱신함
        currentSprite = null;
        cachedTexture = null;
        Repaint();
    }

    private void OnGUI()
    {
        // targetImage가 null이면 자동으로 복구 시도
        if (targetImage == null)
        {
            // 현재 선택된 오브젝트가 MultiSliceImage인지 확인
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                MultiSliceImage image = selected.GetComponent<MultiSliceImage>();
                if (image != null)
                {
                    SetTarget(image);
                }
            }
            
            // 여전히 null이면 경고 표시
            if (targetImage == null)
            {
                EditorGUILayout.HelpBox("No Target Selected.", MessageType.Warning);
                EditorGUILayout.HelpBox("Please select a GameObject with MultiSliceImage component.", MessageType.Info);
                return;
            }
        }
        
        // targetImage가 파괴되었거나 유효하지 않은 경우 처리
        if (targetImage == null || targetImage.Equals(null))
        {
            targetImage = null;
            EditorGUILayout.HelpBox("Target object has been destroyed.", MessageType.Warning);
            return;
        }

        // 컴포넌트의 스프라이트를 실시간으로 가져옴
        Sprite newSprite = targetImage.sprite;
        
        // 스프라이트가 변경되었거나 null인 경우 캐시 초기화
        if (newSprite != currentSprite)
        {
            currentSprite = newSprite;
            cachedTexture = null;
            cachedUV = Vector4.zero;
        }

        if (currentSprite == null)
        {
            EditorGUILayout.HelpBox("No Sprite Assigned in Component.", MessageType.Warning);
            return;
        }

        // 텍스처가 변경되었는지 확인 (아틀라스 리팩킹 후 텍스처 참조가 변경될 수 있음)
        Texture2D newTexture = currentSprite.texture;
        if (newTexture != cachedTexture)
        {
            cachedTexture = newTexture;
            // UV 좌표도 다시 계산
            cachedUV = Vector4.zero;
        }

        DrawToolbar();
        DrawImageArea();
        HandleInput();

        if (GUI.changed)
        {
            // 변경 사항이 생기면 씬 뷰의 컴포넌트도 즉시 갱신
            UpdateImageImmediately();
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        // Vertical Controls
        int vCount = targetImage.verticalCuts.Count;
        GUILayout.Label($"Vertical Cuts: {vCount}/4", GUILayout.Width(100));
        
        bool canAddVertical = vCount < 4;
        EditorGUI.BeginDisabledGroup(!canAddVertical);
        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            Undo.RecordObject(targetImage, "Add Vertical Cut");
            targetImage.verticalCuts.Add(0.5f);
            targetImage.verticalCuts.Sort();
            targetImage.SetVerticesDirty();
        }
        EditorGUI.EndDisabledGroup();
        
        if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            if (vCount > 0)
            {
                Undo.RecordObject(targetImage, "Remove Vertical Cut");
                targetImage.verticalCuts.RemoveAt(vCount - 1);
                targetImage.SetVerticesDirty();
            }
        }

        GUILayout.Space(20);

        // Horizontal Controls
        int hCount = targetImage.horizontalCuts.Count;
        GUILayout.Label($"Horizontal Cuts: {hCount}/4", GUILayout.Width(120));
        
        bool canAddHorizontal = hCount < 4;
        EditorGUI.BeginDisabledGroup(!canAddHorizontal);
        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            Undo.RecordObject(targetImage, "Add Horizontal Cut");
            targetImage.horizontalCuts.Add(0.5f);
            targetImage.horizontalCuts.Sort();
            targetImage.SetVerticesDirty();
        }
        EditorGUI.EndDisabledGroup();
        
        if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            if (hCount > 0)
            {
                Undo.RecordObject(targetImage, "Remove Horizontal Cut");
                targetImage.horizontalCuts.RemoveAt(hCount - 1);
                targetImage.SetVerticesDirty();
            }
        }
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        // 컷 수 제한 경고
        if (vCount > 4 || hCount > 4)
        {
            EditorGUILayout.HelpBox("컷 수는 최대 4개까지 가능합니다. 초과된 컷은 자동으로 제거됩니다.", MessageType.Warning);
        }
    }

    private void DrawImageArea()
    {
        Rect workArea = new Rect(0, 20, position.width, position.height - 20);
        
        // 배경 그리기
        EditorGUI.DrawRect(workArea, new Color(0.15f, 0.15f, 0.15f));

        if (currentSprite == null) return;

        Texture2D tex = currentSprite.texture;
        if (tex == null) return; // 텍스처가 로드되지 않았을 경우 방어

        // 이미지 비율 계산
        float spriteW = currentSprite.rect.width;
        float spriteH = currentSprite.rect.height;
        
        // 너비/높이가 0일 경우 방어
        if (spriteW <= 0 || spriteH <= 0) return;

        float aspect = spriteW / spriteH;

        // 비대칭 패딩: 우측·하단에 라벨(가로 50px / 세로 30px) 표시 공간 확보.
        // 기존 20px 사방 패딩에서는 라벨이 윈도우 가장자리에 잘려 표시되었음.
        const float paddingLeft = 20f;
        const float paddingTop = 20f;
        const float paddingRight = 60f;   // 우측 Height 라벨 공간 ("1000" 등 4자리까지 여유)
        const float paddingBottom = 32f;  // 하단 Width 라벨 공간

        float availW = workArea.width - paddingLeft - paddingRight;
        float availH = workArea.height - paddingTop - paddingBottom;
        if (availW <= 1f) availW = 1f;
        if (availH <= 1f) availH = 1f;

        float drawW, drawH;

        if (availW / availH > aspect)
        {
            drawH = availH;
            drawW = drawH * aspect;
        }
        else
        {
            drawW = availW;
            drawH = drawW / aspect;
        }

        // 사용 가능 영역의 중앙에 이미지 배치
        imageRect = new Rect(
            workArea.x + paddingLeft + (availW - drawW) * 0.5f,
            workArea.y + paddingTop + (availH - drawH) * 0.5f,
            drawW,
            drawH
        );

        // [중요 수정] 아틀라스 UV 좌표를 안전하게 가져오는 방식 변경
        // DataUtility.GetOuterUV는 유니티 내부에서 사용하는 정확한 UV값(Vector4)을 반환합니다.
        // (x: minU, y: minV, z: maxU, w: maxV)
        // 아틀라스 리팩킹 후에도 최신 UV 좌표를 가져오기 위해 매번 다시 계산
        Vector4 uvOuter = UnityEngine.Sprites.DataUtility.GetOuterUV(currentSprite);
        
        // UV가 변경되었는지 확인 (아틀라스 리팩킹으로 인한 변경 감지)
        if (cachedUV != uvOuter)
        {
            cachedUV = uvOuter;
        }
        
        Rect uvRect = new Rect(uvOuter.x, uvOuter.y, uvOuter.z - uvOuter.x, uvOuter.w - uvOuter.y);

        // 텍스처 그리기 (아틀라스 리팩킹 후에도 최신 UV로 정상 표시됨)
        GUI.DrawTextureWithTexCoords(imageRect, tex, uvRect);

        // 외곽선
        Handles.color = Color.white;
        Handles.DrawWireCube(imageRect.center, imageRect.size);

        // 라인 그리기
        DrawLines(targetImage.verticalCuts, true);
        DrawLines(targetImage.horizontalCuts, false);
    }

    private void DrawLines(List<float> cuts, bool isVertical)
    {
        if (currentSprite == null) return;

        float spriteW = currentSprite.rect.width;
        float spriteH = currentSprite.rect.height;

        // 녹색으로 통일
        Handles.color = Color.green;

        // 가이드 라인 그리기 및 위치값 표시
        for (int i = 0; i < cuts.Count; i++)
        {
            float t = cuts[i];
            
            float xPos = isVertical 
                ? imageRect.x + imageRect.width * t 
                : imageRect.x;
            
            float yPos = isVertical 
                ? imageRect.y 
                : imageRect.y + imageRect.height * (1 - t); 

            Vector3 start = new Vector3(xPos, yPos, 0);
            Vector3 end = isVertical 
                ? new Vector3(xPos, imageRect.y + imageRect.height, 0)
                : new Vector3(imageRect.x + imageRect.width, yPos, 0);

            Handles.DrawLine(start, end);

            Rect handleRect;
            if (isVertical)
                handleRect = new Rect(xPos - 4, imageRect.y, 8, imageRect.height);
            else
                handleRect = new Rect(imageRect.x, yPos - 4, imageRect.width, 8);

            EditorGUIUtility.AddCursorRect(handleRect, isVertical ? MouseCursor.ResizeHorizontal : MouseCursor.ResizeVertical);

            // 가이드 라인 위치에 숫자 표시
            if (isVertical)
            {
                // Vertical: 하단에 Width 값 표시
                float widthValue = t * spriteW;
                DrawGuideLabel(xPos, imageRect.yMax + 8, Mathf.RoundToInt(widthValue), true);
            }
            else
            {
                // Horizontal: 오른쪽에 Height 값 표시
                float heightValue = (1 - t) * spriteH; // Y는 아래에서 위로
                DrawGuideLabel(imageRect.xMax + 8, yPos, Mathf.RoundToInt(heightValue), false);
            }
        }
    }

    private void DrawGuideLabel(float x, float y, int value, bool isVertical)
    {
        GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontSize = 10;
        labelStyle.padding = new RectOffset(4, 4, 2, 2);
        labelStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.8f));
        labelStyle.alignment = isVertical ? TextAnchor.UpperCenter : TextAnchor.MiddleLeft;

        string labelText = value.ToString();
        Vector2 textSize = labelStyle.CalcSize(new GUIContent(labelText));
        
        Rect labelRect;
        if (isVertical)
        {
            // Vertical: 하단 중앙 정렬
            labelRect = new Rect(x - textSize.x * 0.5f, y, textSize.x, textSize.y);
        }
        else
        {
            // Horizontal: 오른쪽 중앙 정렬
            labelRect = new Rect(x, y - textSize.y * 0.5f, textSize.x, textSize.y);
        }

        GUI.Label(labelRect, labelText, labelStyle);
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    private void HandleInput()
    {
        Event e = Event.current;

        // 드래그 시작
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            float minDist = 10f;
            draggingLineIndex = -1;

            // Vertical 체크
            for (int i = 0; i < targetImage.verticalCuts.Count; i++)
            {
                float xPos = imageRect.x + imageRect.width * targetImage.verticalCuts[i];
                float dist = Mathf.Abs(e.mousePosition.x - xPos);
                if (dist < minDist && imageRect.Contains(new Vector2(xPos, e.mousePosition.y)))
                {
                    minDist = dist;
                    draggingLineIndex = i;
                    isDraggingVertical = true;
                }
            }

            // Horizontal 체크
            if (draggingLineIndex == -1)
            {
                for (int i = 0; i < targetImage.horizontalCuts.Count; i++)
                {
                    float yPos = imageRect.y + imageRect.height * (1 - targetImage.horizontalCuts[i]);
                    float dist = Mathf.Abs(e.mousePosition.y - yPos);
                    if (dist < minDist && imageRect.Contains(new Vector2(e.mousePosition.x, yPos)))
                    {
                        minDist = dist;
                        draggingLineIndex = i;
                        isDraggingVertical = false;
                    }
                }
            }

            if (draggingLineIndex != -1)
            {
                Undo.RecordObject(targetImage, "Move Slice Line");
                e.Use();
            }
        }

        // 드래그 중
        if (e.type == EventType.MouseDrag && draggingLineIndex != -1)
        {
            if (isDraggingVertical)
            {
                float t = (e.mousePosition.x - imageRect.x) / imageRect.width;
                targetImage.verticalCuts[draggingLineIndex] = Mathf.Clamp01(t);
            }
            else
            {
                float t = 1 - (e.mousePosition.y - imageRect.y) / imageRect.height;
                targetImage.horizontalCuts[draggingLineIndex] = Mathf.Clamp01(t);
            }
            
            // Editor에서 즉시 반영
            UpdateImageImmediately();
            e.Use();
            GUI.changed = true;
        }

        // 드래그 종료
        if (e.type == EventType.MouseUp && draggingLineIndex != -1)
        {
            draggingLineIndex = -1;
            targetImage.verticalCuts.Sort();
            targetImage.horizontalCuts.Sort();
            
            // 정렬 후에도 즉시 반영
            UpdateImageImmediately();
            e.Use();
        }
    }

    private void UpdateImageImmediately()
    {
        if (targetImage == null) return;

        // 캐시 무효화를 위해 stopsDirty 플래그 설정
        // MultiSliceImage에 public 메서드가 없으므로 SetVerticesDirty로 강제 갱신
        targetImage.SetVerticesDirty();
        targetImage.SetMaterialDirty();
        
        // Editor에서 변경사항을 즉시 반영
        EditorUtility.SetDirty(targetImage);
        
        // 씬 뷰와 게임 뷰를 즉시 리페인트
        SceneView.RepaintAll();
        
        // Editor Window도 리페인트
        Repaint();
    }
}
#endif