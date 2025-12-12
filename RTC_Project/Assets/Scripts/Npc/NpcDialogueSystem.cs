using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPEffects.Components;
using TMPro;
using UnityEngine;

public class NpcDialogueSystem : MonoBehaviour
{
    [Header("UI / Bubble")]
    public TextBubbleSystem textBubble; // 사용중인 텍스트 버블 컴포넌트
    private TMP_Text context;            // textBubble.context 를 미리 연결해도 됨

    [Header("State")]
    public string speakerName;      // 이 NPC의 speaker 이름 (CSV의 speaker 컬럼)
    public bool wantTalk = false;   // 상호작용을 원하는지
    public bool inTalking = false;  // 현재 대화중인지
    public bool isTyping = false; // 현재 타이핑 중인지
    
    public bool skiped = false;
    public bool playerLeave = false; // 플레이어가 대화 도중 자리 이탈
    
    [Header("Data")]
    public DialogueDatabase database; // 대화 DB 할당
    private Dictionary<string, DialogueAsset> dialogueMap = new Dictionary<string, DialogueAsset>();

    private Coroutine runningCoroutine;

    private void Start()
    {
        StartCoroutine(TalkPossibleDisplay());
        context = textBubble.context;
        BuildMapFromDatabase();
    }

    //플레이어쪽에서 호출함 (상호작용 버튼을 눌렀을때)
    public void Talking()
    {
        if (inTalking) return;
        
        // speakerName에 해당하는 시작 대사 찾기
        var startAsset = FindStartAssetForSpeaker(speakerName);
        if (startAsset == null)
        {
            Debug.LogWarning($"대사 없음: speaker={speakerName}");
            return;
        }
        
        inTalking = true;
        textBubble.Hide();
        
        runningCoroutine = StartCoroutine(RunDialogueSequence(startAsset));
   }
    
    private IEnumerator RunDialogueSequence(DialogueAsset start)
    {
        DialogueAsset current = start;

        while (current != null)
        {
            // 조건 체크
            if (!CheckCondition(current.condition))
            {
                if (string.IsNullOrEmpty(current.nextId))
                {
                    EndDialogue(current);
                    yield break;
                }
                else
                {
                    dialogueMap.TryGetValue(current.nextId, out current);
                    continue;
                }
            }

            // 텍스트 출력 전 처리: 숨기기 -> 0.5s -> Show 애니메이션
            if (textBubble != null)
            {
                context.text = "";
                textBubble.Hide();
            }
            yield return new WaitForSeconds(0.3f);
            if (textBubble != null)
            {
                textBubble.gameObject.SetActive(true);
            }

            // 실제 텍스트 세팅 (Localization이 있다면 LocalizationManager로 바꿔 호출)
            yield return StartCoroutine(TypeText(current.text));
            
            //AudioManager.Play(current.voiceKey);
            //Animator.SetTrigger(current.emotion);

            // duration 만큼 대기
            float wait = Mathf.Max(0.01f, int.Parse(current.duration));
            yield return new WaitForSeconds(wait);

            if (playerLeave)
            {
                DialogueAsset leaveLine = FindLeaveDialogue();
                if (leaveLine != null)
                {
                    current = leaveLine;
                    playerLeave = false; // leave 처리 완료 후 초기화
                    continue; // leave 대사부터 다시 시작
                }
            }
            
            // 다음으로 이동
            if (string.IsNullOrEmpty(current.nextId))
            {
                // 대화 종료
                EndDialogue(current);
                yield break;
            }
            else
            {
                if (!dialogueMap.TryGetValue(current.nextId, out current))
                {
                    Debug.LogWarning($"다음 대사를 찾을 수 없음: nextId={current.nextId}");
                    EndDialogue(current);
                    yield break;
                }
            }
        }

        // 루프 종료 시 안전하게 정리
        EndDialogue(current);
    }
    
    private IEnumerator TypeText(string fullText, float speed = 0.075f)
    {
        isTyping = true;
        context.text = "";

        int i = 0;

        while (i < fullText.Length)
        {
            // 스킵 입력 감지
            if (skiped)
            {
                skiped = false;
                context.text = fullText; // 즉시 전체 출력
                isTyping = false;
                yield break;
            }

            // TMP 태그 감지
            if (fullText[i] == '<')
            {
                int tagEnd = fullText.IndexOf('>', i);
                if (tagEnd != -1)
                {
                    string tag = fullText.Substring(i, tagEnd - i + 1);
                    context.text += tag;
                    i = tagEnd + 1;
                    continue;
                }
            }

            // 일반 문자 타이핑
            context.text += fullText[i];
            i++;

            yield return new WaitForSeconds(speed);
        }

        isTyping = false;
    }
    
    private IEnumerator TalkPossibleDisplay()
    {
        if(!wantTalk) yield break;
        
        yield return new WaitForSeconds(0.5f);
        textBubble.context.text = "...";
        textBubble.gameObject.SetActive(true);
    }
    
    private void EndDialogue(DialogueAsset lastLine)
    {
        if (textBubble != null) textBubble.Hide();

        context.text = "";
        inTalking = false;

        // ◆ note가 null 또는 빈 문자열이면 다시 대화 가능
        if (string.IsNullOrEmpty(lastLine.note) || lastLine.note == "null")
        {
            wantTalk = true;
            StartCoroutine(TalkPossibleDisplay());
        }
        else
        {
            wantTalk = false;
        }

        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }
    }
    
    #region ----HELPER----
    private DialogueAsset FindStartAssetForSpeaker(string speaker)
    {
        // speaker 모든 대사 수집
        var speakerAssets = dialogueMap.Values.Where(a => a != null && a.speaker == speaker).ToList();
        if (speakerAssets.Count == 0) return null;

        // 같은 그룹 내에서 nextId로 참조된 id들을 모아 시작점을 찾음
        var referenced = new HashSet<string>(speakerAssets
            .Where(a => !string.IsNullOrEmpty(a.nextId))
            .Select(a => a.nextId));

        // 시작 대사: referenced에 포함되지 않는 id 중 하나 (없으면 id 기준 정렬된 첫 항목)
        var candidate = speakerAssets.FirstOrDefault(a => !referenced.Contains(a.id));
        if (candidate != null) return candidate;

        // 안전망: 사전순으로 제일 작은 것 반환
        return speakerAssets.OrderBy(a => a.id).FirstOrDefault();
    }
    
    private void BuildMapFromDatabase()
    {
        dialogueMap.Clear();
        if (database == null || database.dialogueAssets == null) return;

        foreach (var asset in database.dialogueAssets)
        {
            if (asset == null || string.IsNullOrEmpty(asset.id)) continue;
            if (!dialogueMap.ContainsKey(asset.id))
                dialogueMap.Add(asset.id, asset);
            else
                Debug.LogWarning($"DialogueDatabase: 중복 id 발견 -> {asset.id}");
        }
    }
    
    /// <summary>
    /// 대사 실행 조건을 체크합니다.
    /// </summary>
    private bool CheckCondition(string condition)
    {
        if (string.IsNullOrEmpty(condition) || condition == "normal")
            return true;

        if (condition == "leave")
            return !playerLeave;

        return true; // 기본은 통과
    }

    private DialogueAsset FindLeaveDialogue()
    {
        foreach (var d in dialogueMap.Values)
        {
            if (d.condition == "leave")
                return d;
        }
        return null;
    }
    #endregion
    
    public void SwitchStayText(bool canInteract)
    {
        if (canInteract)
        {
            context.text = "";
            textBubble.PlayShowAnimation();
            context.text = "!";
        }
        else
        {
            context.text = "";
            textBubble.PlayShowAnimation();
            context.text = "...";
        }
    }
}
