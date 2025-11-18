using UnityEngine;
using TMPro;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("CAT/TMP/UITMPCurve")]
public class UITMPCurve : MonoBehaviour
{
    public AnimationCurve vertexCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.25f, 2.0f),
        new Keyframe(0.5f, 0), new Keyframe(0.75f, 2.0f), new Keyframe(1, 0f));
    public float angleMultiplier = 1.0f;
    public float speedMultiplier = 1.0f;
    public float curveScale = 1.0f;

    private TMP_Text textComponent;
    private Coroutine warpTextCoroutine;

    void OnEnable()
    {
        if (textComponent == null)
            textComponent = GetComponent<TMP_Text>();

        ApplyWarpText();
    }

    void OnDisable()
    {
        if (warpTextCoroutine != null)
        {
            StopCoroutine(warpTextCoroutine);
            warpTextCoroutine = null;
        }
    }

    public void ApplyWarpText()
    {
        if (textComponent == null)
            textComponent = GetComponent<TMP_Text>();

        if (warpTextCoroutine != null)
        {
            StopCoroutine(warpTextCoroutine);
            warpTextCoroutine = null;
        }

        warpTextCoroutine = StartCoroutine(DoWarpText());
    }

    private AnimationCurve CopyAnimationCurve(AnimationCurve curve)
    {
        AnimationCurve newCurve = new AnimationCurve();
        newCurve.keys = curve.keys;
        return newCurve;
    }

    IEnumerator DoWarpText()
    {
        vertexCurve.preWrapMode = WrapMode.Clamp;
        vertexCurve.postWrapMode = WrapMode.Clamp;

        textComponent.havePropertiesChanged = true;

        Vector3[] vertices;
        Matrix4x4 matrix;

        float old_CurveScale = curveScale;
        AnimationCurve old_curve = CopyAnimationCurve(vertexCurve);

        while (true)
        {
            if (!textComponent.havePropertiesChanged && old_CurveScale == curveScale &&
                old_curve.keys[1].value == vertexCurve.keys[1].value)
            {
                yield return new WaitForSeconds(0.025f);
                continue;
            }

            old_CurveScale = curveScale;
            old_curve = CopyAnimationCurve(vertexCurve);

            textComponent.ForceMeshUpdate();

            TMP_TextInfo textInfo = textComponent.textInfo;
            int characterCount = textInfo.characterCount;

            if (characterCount == 0)
            {
                yield return new WaitForSeconds(0.025f);
                continue;
            }

            float boundsMinX = textComponent.bounds.min.x;
            float boundsMaxX = textComponent.bounds.max.x;

            for (int i = 0; i < characterCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible)
                    continue;

                int vertexIndex = textInfo.characterInfo[i].vertexIndex;
                int materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
                vertices = textInfo.meshInfo[materialIndex].vertices;

                Vector3 offsetToMidBaseline = new Vector2((vertices[vertexIndex + 0].x + vertices[vertexIndex + 2].x) / 2,
                    textInfo.characterInfo[i].baseLine);

                vertices[vertexIndex + 0] += -offsetToMidBaseline;
                vertices[vertexIndex + 1] += -offsetToMidBaseline;
                vertices[vertexIndex + 2] += -offsetToMidBaseline;
                vertices[vertexIndex + 3] += -offsetToMidBaseline;

                float x0 = (offsetToMidBaseline.x - boundsMinX) / (boundsMaxX - boundsMinX);
                float x1 = x0 + 0.0001f;
                float y0 = vertexCurve.Evaluate(x0) * curveScale;
                float y1 = vertexCurve.Evaluate(x1) * curveScale;

                Vector3 horizontal = new Vector3(1, 0, 0);
                Vector3 tangent = new Vector3(x1 * (boundsMaxX - boundsMinX) + boundsMinX, y1) -
                    new Vector3(offsetToMidBaseline.x, y0);
                Vector3 cross = Vector3.Cross(horizontal, tangent);

                float dot = Mathf.Acos(Vector3.Dot(horizontal, tangent.normalized)) * 57.2957795f;
                float angle = (cross.z > 0 ? dot : 360 - dot) * angleMultiplier;

                matrix = Matrix4x4.TRS(new Vector3(0, y0, 0), Quaternion.Euler(0, 0, angle), Vector3.one);

                vertices[vertexIndex + 0] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 0]);
                vertices[vertexIndex + 1] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 1]);
                vertices[vertexIndex + 2] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 2]);
                vertices[vertexIndex + 3] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 3]);

                vertices[vertexIndex + 0] += offsetToMidBaseline;
                vertices[vertexIndex + 1] += offsetToMidBaseline;
                vertices[vertexIndex + 2] += offsetToMidBaseline;
                vertices[vertexIndex + 3] += offsetToMidBaseline;
            }

            textComponent.UpdateVertexData();

            yield return new WaitForSeconds(0.025f * speedMultiplier);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && gameObject.activeInHierarchy)
        {
            ApplyWarpText();
        }
    }
#endif
}