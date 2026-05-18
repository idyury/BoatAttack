using BoatAttack;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DemoRun : MonoBehaviour
{
    private void Start()
    {
        Application.targetFrameRate = 60;
        Debug.Log($"Set targetFrameRate {Application.targetFrameRate}. Resolution {Screen.currentResolution}");
        StartCoroutine(RaceManager.SetupRace());
    }
}
