using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


public class PlayerAssetsInputs : MonoBehaviour
{
	[Header("Character Input Values")]
	[HideInInspector] public Vector2 look;

	[Header("Mouse Cursor Settings")]
	public bool cursorLocked = true;
	public bool cursorInputForLook = true;

	[HideInInspector] public Vector2 move = Vector2.zero;
	[HideInInspector] public bool wantJump = false;
	
#if ENABLE_INPUT_SYSTEM
	
	void OnMove(InputValue value)
	{
		move = value.Get<Vector2>();
	}

	void OnJump(InputValue value)
	{
		if (value.isPressed) wantJump = true;
	}
	
	public void OnLook(InputValue value)
	{
		if(cursorInputForLook)
		{
			LookInput(value.Get<Vector2>());
		}
	}
	
#endif

	public void LookInput(Vector2 newLookDirection)
	{
		look = newLookDirection;
	}
	
	private void OnApplicationFocus(bool hasFocus)
	{
		SetCursorState(cursorLocked);
	}

	private void SetCursorState(bool newState)
	{
		Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
	}
}
