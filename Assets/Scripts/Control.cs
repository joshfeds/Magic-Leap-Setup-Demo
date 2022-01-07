using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.MagicLeap;
public class Control : MonoBehaviour {

  #region Public Variables  
  public enum Mode {LOOSE, SOFT, HARD};
  public Mode WorldMode;
  public Material FrameMat;
  public GameObject WorldCanvas;
  public GameObject Earth;
  public GameObject Light;
  public GameObject Camera;
  #endregion
  
  #region Private Variables
  private HeadLockScript _headlock;
  private const float _triggerThreshold = 0.2f;
  private const float _rotspeed = 10.0f;
  private bool _triggerPressed = false;
  private MLInput.Controller _control;
  #endregion
  
  #region Unity Methods
  private void Start() {
    // Get the HeadLockScript script
    _headlock = GetComponentInChildren<HeadLockScript>();
    // Setup and Start Magic Leap input and add a button event (will be used for the HomeTap)
    MLInput.Start();
    MLInput.OnControllerButtonUp += OnButtonUp;
    _control = MLInput.GetController(MLInput.Hand.Left);
    // Reset the scene
    Reset();
  }
  private void Update() {
    // Check the inputs and update the scene
    CheckControl();
    
    // Update the head lock state
    CheckStates();
    
    // Update the earth model and light
    EarthRotationAndLight();
  }
  private void OnDestroy () {
    MLInput.OnControllerButtonUp -= OnButtonUp;
    MLInput.Stop();
  }
  #endregion
  
  #region Private Methods
  /// CheckStates
  /// Switch headlock mode depending on the world mode
  ///
  private void CheckStates() {
    if (WorldMode == Mode.LOOSE) {
      _headlock.HeadLock(WorldCanvas, 1.75f);
      FrameMat.color = Color.red;
    }
    else if (WorldMode == Mode.SOFT) {
      _headlock.HeadLock(WorldCanvas, 5.0f);
      FrameMat.color = Color.green;
     }
     else {
       _headlock.HardHeadLock(WorldCanvas);
       FrameMat.color = Color.blue;
     }
   }
   
   /// EarthRotationAndLight
   /// Rotate the earth model and set the position and rotation of the light
   ///
   private void EarthRotationAndLight() {   
     Earth.transform.Rotate(Vector3.up, - _rotspeed * Time.deltaTime);
     Light.transform.position = Camera.transform.position;
     Light.transform.rotation = Camera.transform.rotation;
   }
    
   /// Reset
   /// Resets the scene back to the starting Instruction screen
   ///    
   private void Reset() {
     WorldMode = Mode.LOOSE;
   }
   
   /// CheckControl
   /// Monitor the trigger input to "increment" the  world mode
   ///
   private void CheckControl() {
     if (_control.TriggerValue > _triggerThreshold) {
       _triggerPressed = true;
     }
     else if (_control.TriggerValue == 0.0f && _triggerPressed) {
       _triggerPressed = false;
       if (WorldMode == Mode.LOOSE) {
         WorldMode = Mode.SOFT;
       }
       else if (WorldMode == Mode.SOFT) {
         WorldMode = Mode.HARD;
       }
       else {
         WorldMode = Mode.LOOSE;
       }
     }
   }

   /// OnButtonUp
   /// Button event - reset scene when home button is tapped
   ///
   private void OnButtonUp(byte controller_id, MLInput.Controller.Button button) {
     if (button == MLInput.Controller.Button.HomeTap) {
       Reset();
     }
   }
   #endregion
}
