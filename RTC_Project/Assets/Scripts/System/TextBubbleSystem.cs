using UnityEngine;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;
using System;
using TMPEffects.Components;

public class TextBubbleSystem : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer bubbleSr;        // Sliced SpriteRenderer (size 사용)
    public SpriteRenderer taleSr; 
    public TMP_Text context;         // TextMeshPro 텍스트 (RectTransform)
    //[HideInInspector]public TMPAnimator tmpAnimator;
    //private TMPWriter tmpWriter;

    [Header("Resize")]
    public Vector2 padding = new Vector2(0.35f, 0.35f); 
    private float resizeDuration = 0.18f;

    //[Header("Animation")]
    private float showDuration = 0.25f;
    private float showStartAlpha = 0.4f;
    private float hideDuration = 0.18f;
    private float hideEndAlpha = 0f;

    [Header("Misc")]
    public Vector2 minSize = new Vector2(0.66f, 0.66f); // 최소 사이즈(유닛)
    
    // 내부
    private Tween sizeTween;
    private Tween taleTween;
    private Tween showTween;
    private Tween hideTween;
    
    private Color bubbleColor;
    private Color taleColor;
    private Color textColor;
    
    private RectTransform textRect;
    private RectTransform bubbleRect;
    private Vector3 bubbleLocalPos;
    private Vector3 taleOriginalScale;
    private Vector2 talelastSize = Vector2.zero;
    
    private bool isHiding = false;
    
    [Header("TextLook")]
    public Transform npc;        // 꼬리가 바라볼 NPC
    public Transform taleRoot;   // 꼬리 루트 (Z축 회전만 할 것)
    private Camera cam;

    private void Awake()
    {
        bubbleLocalPos = bubbleSr.transform.localPosition;
        bubbleRect = bubbleSr.gameObject.GetComponent<RectTransform>();
        textRect = context.rectTransform;
        bubbleColor = bubbleSr.color;
        taleColor = taleSr.color;
        textColor = context.color;
        //tmpAnimator = context.GetComponent<TMPAnimator>();
        //tmpWriter = context.GetComponent<TMPWriter>();
        cam = Camera.main;
    }

    private void OnEnable()
    {
        PlayShowAnimation();
    }

    private void LateUpdate()
    {
        TextUpdateAction();
        TextLookAction();
    }

    private void OnDisable()
    {
        // 비활성화될 때 트윈 정리
        KillAllTweens();
    }

    //말풍선 닫기 -> 외부 호출
    public void Hide()
    {
        if (isHiding) return;
        isHiding = true;
        PlayHideAnimation(() =>
        {
            // 애니메이션 끝나면 완전 비활성화
            //tmpAnimator.enabled = false;
            isHiding = false;
            //tmpWriter.ResetWriter();
            gameObject.SetActive(false);
        });
    }
    
    private void TextUpdateAction()
    {
        if (textRect == null) return;
        
        //레이아웃을 강제 갱신 -> ContentSizeFitter 반영
        LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        
        //텍스트 크기 목표 sr.size계산
        Vector2 targetPixelSize = Vector2.zero;
        targetPixelSize = new Vector2(textRect.rect.width + padding.x, textRect.rect.height + padding.y);
        bubbleRect.sizeDelta = new Vector2(textRect.rect.width + padding.x, textRect.rect.height + padding.y);

        // 최소 크기 값
        targetPixelSize.x = Mathf.Max(minSize.x, targetPixelSize.x);
        targetPixelSize.y = Mathf.Max(minSize.y, targetPixelSize.y);

        // 부드럽게 Tween으로 변경
        if (sizeTween != null && sizeTween.IsActive()) sizeTween.Kill();
        sizeTween = DOTween.To(() => bubbleSr.size, x => bubbleSr.size = x, targetPixelSize, resizeDuration)
            .SetEase(Ease.OutQuad);
        
        // 텍스트 크기 변화 감지
        if (Vector2.Distance(talelastSize, targetPixelSize) > 0.01f)
        {
            PlayTailPopAnimation();
        }

        talelastSize = targetPixelSize;
    }

    /// <summary>
    /// 텍스트가 변화할때 꼬리 튕김
    /// </summary>
    private void PlayTailPopAnimation()
    {
        if (taleOriginalScale == Vector3.zero)
            taleOriginalScale = taleSr.transform.localScale;

        if (taleTween != null && taleTween.IsActive())
            taleTween.Kill();

        Vector3 popScale = taleOriginalScale + new Vector3(0, 0.07f, 0);

        taleTween = taleSr.transform.DOScale(popScale, resizeDuration * 0.5f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                taleSr.transform.DOScale(taleOriginalScale, resizeDuration * 0.5f)
                    .SetEase(Ease.InQuad);
            });
    }

    /// <summary>
    /// 말풍선 내용 -> 항상 카메라 바라보기 |
    /// 말풍선 꼬리 -> 항상 화자를 가리키기
    /// </summary>
    private void TextLookAction()
    {
        if (!cam || !npc) return;
        
        // 1) 텍스트버블 전체는 카메라를 바라봄 (텍스트)
        context.transform.rotation = Quaternion.LookRotation(context.transform.position - cam.transform.position);
        
        // 2) 꼬리는 NPC 방향을 향하도록 Z축 회전만 적용
        Vector3 npcDir = npc.position - taleRoot.position;
        float zAngle = Mathf.Atan2(npcDir.y, npcDir.x) * Mathf.Rad2Deg + 90f;
        Quaternion zQ = Quaternion.Euler(0, 0, zAngle);
        
        Vector3 camDir = cam.transform.position - taleRoot.transform.position;
        camDir.y = 0f; // 수평 성분만 사용
        if (camDir.sqrMagnitude < 0.0001f) return; // 안전막
        float yaw = Mathf.Atan2(camDir.x, camDir.z) * Mathf.Rad2Deg;
        Quaternion yawQ = Quaternion.Euler(0f, yaw, 0f);
        
        taleRoot.transform.rotation =  zQ * yawQ;
    }
    
    
    //[Aniamtion]--------------------------------------
    public void PlayShowAnimation()
    {
        //tmpAnimator.enabled = true;
        //tmpWriter.StartWriter();
        
        KillAllTweens();
        isHiding = false;

        // 초기 scale
        bubbleSr.transform.localScale = Vector3.one * 0.55f;

        // 알파 초기화
        SetAlpha(bubbleSr, showStartAlpha);
        SetAlpha(taleSr, 0);//showStartAlpha
        SetTextAlpha(showStartAlpha);

        showTween = DOTween.Sequence()
            .Join(bubbleSr.transform.DOScale(1f, showDuration).SetEase(Ease.OutBack))
            .Join(bubbleSr.DOFade(bubbleColor.a, showDuration))
            .Join(context.DOFade(textColor.a, showDuration))
            .Join(taleSr.DOFade(taleColor.a, showDuration).SetDelay(0.085f));
    }

    private void PlayHideAnimation(Action onComplete = null)
    {
        // 기존 Tween 제거
        if (hideTween != null && hideTween.IsActive()) hideTween.Kill();
        if (showTween != null && showTween.IsActive()) showTween.Kill();

        hideTween = DOTween.Sequence()
            .Join(bubbleSr.transform.DOScale(0f, hideDuration).SetEase(Ease.InQuad))
            .Join(bubbleSr.DOFade(hideEndAlpha, hideDuration).SetEase(Ease.InQuad))
            .Join(taleSr.DOFade(hideEndAlpha, hideDuration-0.08f).SetEase(Ease.InQuad))
            .Join(context.DOFade(hideEndAlpha, hideDuration).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                bubbleSr.transform.localScale = Vector3.one;
                bubbleSr.transform.localPosition = bubbleLocalPos;
                bubbleSr.color = bubbleColor;
                taleSr.color = taleColor;
                context.color = textColor;

                onComplete?.Invoke();
            });
    }

    private void KillAllTweens()
    {
        if (sizeTween != null) sizeTween.Kill();
        if (showTween != null) showTween.Kill();
        if (hideTween != null) hideTween.Kill();
    }
    
    private void SetAlpha(SpriteRenderer sr, float a)
    {
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }

    private void SetTextAlpha(float a)
    {
        Color c = context.color;
        c.a = a;
        context.color = c;
    }
}
