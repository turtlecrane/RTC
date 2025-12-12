using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerInteractSystem : MonoBehaviour
{
    public bool canInteract = false;
    private PlayerAssetsInputs _input;
    private Animator anim;
    private NpcDialogueSystem npcScript;
    
    private void Start()
    {
        _input = GetComponent<PlayerAssetsInputs>();
        anim = GetComponent<Animator>();
    }
    
    //플레이어 인풋 매니저에서 호출
    public void OnInteract(InputValue value)
    {
        if (!canInteract)
        {
            Debug.Log("상호작용할 대상이 없음");
            return;
        }
        
        if (value.isPressed)
        {
            Debug.Log("상호작용 버튼이 눌림");
            npcScript.Talking();
            anim.SetTrigger("Talk");
            if (npcScript.isTyping) npcScript.skiped = true; //타이핑 스킵
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("NPC"))
        {
            if (other.GetComponent<NpcDialogueSystem>() != null)
            {
                npcScript = other.GetComponent<NpcDialogueSystem>();
                canInteract = npcScript.wantTalk ? true : false;
                if (npcScript.wantTalk && !npcScript.inTalking)
                    npcScript.SwitchStayText(canInteract);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("NPC"))
        {
            canInteract = false;
            if (other.GetComponent<NpcDialogueSystem>() != null)
            {
                if (npcScript.inTalking)
                {
                    npcScript.playerLeave = true;
                    npcScript = null;
                    //npcScript.StopTalking();
                }
                else
                {
                    npcScript.SwitchStayText(canInteract);
                }
            }
        }
    }
}
