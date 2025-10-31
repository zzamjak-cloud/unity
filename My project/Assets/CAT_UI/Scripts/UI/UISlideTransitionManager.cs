using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 여러 UISlideTransitionController를 그룹으로 관리하고,
/// 상황별로 필요한 연출들만 선택적으로 PlayIn/PlayOut 처리하는 관리자.
/// </summary>
public class UISlideTransitionManager : MonoBehaviour
{
    [System.Serializable]
    public class TransitionEntry
    {

        [Tooltip("이 Slide를 분류하는 태그 (헤더, 프로필, 메뉴버튼, 하단버튼 등)")]
        public string tag; // 하나의 Slide는 하나의 Tag만 가짐
        public UISlideTransitionController controller;
    }

    [System.Serializable]
    public class TransitionPreset
    {
        public string presetName; // 프리셋 이름 (예: "최초 로비 진입", "하단 메뉴 클릭")
        public List<string> playInTags = new List<string>(); // PlayIn할 태그 목록
        public List<string> playOutTags = new List<string>(); // PlayOut할 태그 목록
        public bool includeUnlisted = false; // 목록에 없는 태그들도 처리할지 여부
        public TransitionMode unlistedMode = TransitionMode.Ignore; // 목록에 없는 태그들의 동작 방식
    }

    public enum TransitionMode
    {
        Ignore,    // 무시
        PlayIn,    // PlayIn
        PlayOut    // PlayOut
    }

    [Header("Registered Transitions")]
    [SerializeField] private List<TransitionEntry> transitions = new List<TransitionEntry>();

    [Header("Presets")]
    [SerializeField] private List<TransitionPreset> presets = new List<TransitionPreset>();

    private void OnValidate()
    {
        // 태그 자동 생성: 컨트롤러 이름 기반
        for (int i = 0; i < transitions.Count; i++)
        {
            var entry = transitions[i];
            if (entry.controller == null) continue;

            // 태그가 비어있으면 컨트롤러 이름을 태그로 사용
            if (string.IsNullOrEmpty(entry.tag))
            {
                entry.tag = entry.controller.gameObject.name;
            }
        }
    }

    /// <summary>
    /// 특정 태그를 가진 모든 연출들에 PlayIn 실행
    /// </summary>
    public void PlayInByTag(string tag)
    {
        PlayInByTags(new List<string> { tag });
    }

    /// <summary>
    /// 특정 태그들을 가진 모든 연출들에 PlayIn 실행
    /// </summary>
    public void PlayInByTags(List<string> tags)
    {
        if (tags == null || tags.Count == 0) return;

        foreach (var entry in transitions)
        {
            if (entry.controller == null) continue;
            if (!string.IsNullOrEmpty(entry.tag) && tags.Contains(entry.tag))
            {
                entry.controller.PlayIn();
            }
        }
    }

    /// <summary>
    /// 특정 태그를 가진 모든 연출들에 PlayOut 실행
    /// </summary>
    public void PlayOutByTag(string tag)
    {
        PlayOutByTags(new List<string> { tag });
    }

    /// <summary>
    /// 특정 태그들을 가진 모든 연출들에 PlayOut 실행
    /// </summary>
    public void PlayOutByTags(List<string> tags)
    {
        if (tags == null || tags.Count == 0) return;

        foreach (var entry in transitions)
        {
            if (entry.controller == null) continue;
            if (!string.IsNullOrEmpty(entry.tag) && tags.Contains(entry.tag))
            {
                entry.controller.PlayOut();
            }
        }
    }

    /// <summary>
    /// 프리셋 이름으로 연출 실행
    /// </summary>
    public void PlayPreset(string presetName)
    {
        var preset = presets.FirstOrDefault(p => p.presetName == presetName);
        if (preset == null)
        {
            Debug.LogWarning($"[UISlideTransitionManager] 프리셋을 찾을 수 없습니다: {presetName}");
            return;
        }

        PlayPreset(preset);
    }

    /// <summary>
    /// 프리셋으로 연출 실행
    /// </summary>
    public void PlayPreset(TransitionPreset preset)
    {
        if (preset == null) return;

        foreach (var entry in transitions)
        {
            if (entry.controller == null) continue;
            if (string.IsNullOrEmpty(entry.tag)) continue;

            bool hasPlayInTag = preset.playInTags != null && preset.playInTags.Contains(entry.tag);
            bool hasPlayOutTag = preset.playOutTags != null && preset.playOutTags.Contains(entry.tag);

            if (hasPlayInTag)
            {
                entry.controller.PlayIn();
            }
            else if (hasPlayOutTag)
            {
                entry.controller.PlayOut();
            }
            else if (preset.includeUnlisted)
            {
                // 목록에 없는 태그들 처리
                switch (preset.unlistedMode)
                {
                    case TransitionMode.PlayIn:
                        entry.controller.PlayIn();
                        break;
                    case TransitionMode.PlayOut:
                        entry.controller.PlayOut();
                        break;
                    case TransitionMode.Ignore:
                    default:
                        // 아무것도 하지 않음
                        break;
                }
            }
        }
    }

    /// <summary>
    /// 모든 등록된 연출들에 PlayIn 실행
    /// </summary>
    public void PlayInAll()
    {
        foreach (var entry in transitions)
        {
            if (entry.controller == null) continue;
            entry.controller.PlayIn();
        }
    }

    /// <summary>
    /// 모든 등록된 연출들에 PlayOut 실행
    /// </summary>
    public void PlayOutAll()
    {
        foreach (var entry in transitions)
        {
            if (entry.controller == null) continue;
            entry.controller.PlayOut();
        }
    }


    /// <summary>
    /// 런타임에 연출을 등록
    /// </summary>
    public void RegisterTransition(UISlideTransitionController controller, string tag = null)
    {
        if (controller == null) return;

        var existing = transitions.FirstOrDefault(e => e.controller == controller);
        if (existing != null)
        {
            // 기존 항목 업데이트
            if (!string.IsNullOrEmpty(tag)) existing.tag = tag;
            
            // 태그가 비어있으면 컨트롤러 이름을 태그로 사용
            if (string.IsNullOrEmpty(existing.tag))
            {
                existing.tag = controller.gameObject.name;
            }
        }
        else
        {
            // 새 항목 추가
            string entryTag = tag ?? controller.gameObject.name;
            
            var entry = new TransitionEntry
            {
                controller = controller,
                tag = entryTag
            };
            transitions.Add(entry);
        }
    }

    /// <summary>
    /// 런타임에 연출 등록 해제
    /// </summary>
    public void UnregisterTransition(UISlideTransitionController controller)
    {
        transitions.RemoveAll(e => e.controller == controller);
    }

    /// <summary>
    /// 등록된 모든 프리셋 이름 목록 가져오기
    /// </summary>
    public List<string> GetPresetNames()
    {
        return presets.Select(p => p.presetName).ToList();
    }

    /// <summary>
    /// 프리셋 추가 (런타임)
    /// </summary>
    public void AddPreset(TransitionPreset preset)
    {
        if (preset != null && !presets.Any(p => p.presetName == preset.presetName))
        {
            presets.Add(preset);
        }
    }
}

