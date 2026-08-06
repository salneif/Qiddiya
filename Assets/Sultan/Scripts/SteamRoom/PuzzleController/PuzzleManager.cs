using UnityEngine;
using System;

public class PuzzleManager : MonoBehaviour
{
    [SerializeField] private Lever[] levers;
    [SerializeField] private ClockFace clock;
    [SerializeField] private float targetMinutes = 180f;
    [SerializeField] private float tolerance = 2f;
    [SerializeField] private bool debug;
    [SerializeField] private Transform platform;
    [SerializeField] private PlatformRiseStop platformStop;
    [SerializeField] private float rotateSpeed = 30f;
    [SerializeField] private float riseSpeed = 0.5f;
    [SerializeField] private float riseHeight = 20f;
    [SerializeField] private AudioSource platformAudio;
    [SerializeField] private AudioClip startSound;
    [SerializeField] private AudioClip riseLoop;
    [SerializeField] private GameObject collider1;
    [SerializeField] private GameObject collider2;
    [SerializeField] private GameObject collider3;
    [SerializeField] private GameObject collider4;

    public event Action OnPuzzleSolved;

    private bool _solved;
    private bool _riseComplete;
    private float _risen;

    void OnEnable()
    {
        foreach (var l in levers)
            l.OnLeverChanged += onLeverChanged;
    }

    void OnDisable()
    {
        foreach (var l in levers)
            l.OnLeverChanged -= onLeverChanged;
    }

    void Update()
    {
        if (!_solved || platform == null || _riseComplete) return;

        if (platformStop != null && platformStop.Blocked)
        {
            finishRise();
            return;
        }

        platform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f);

        float step = riseSpeed * Time.deltaTime;
        if (_risen + step >= riseHeight)
        {
            platform.position += Vector3.up * (riseHeight - _risen);
            _risen = riseHeight;
            finishRise();
            return;
        }

        platform.position += Vector3.up * step;
        _risen += step;
    }

    void finishRise()
    {
        _riseComplete = true;
        stopRiseLoop();
    }

    void onLeverChanged(Lever lever)
    {
        if (_solved) return;

        float total = 0f;
        foreach (var l in levers)
            total += l.PressureContribution;

        if (debug)
            Debug.Log($"[Puzzle] {lever.ID} {lever.State} | total={total} mins");

        clock.SetMinutes(total);

        if (Mathf.Abs(total - targetMinutes) <= tolerance)
        {
            _solved = true;
            Debug.Log("SOLVED");
            collider1.SetActive(true);
            collider2.SetActive(true);
            collider3.SetActive(true);
            collider4.SetActive(true);
            playPlatformAudio();
            OnPuzzleSolved?.Invoke();
        }
    }

    void playPlatformAudio()
    {
        if (platformAudio == null) return;

        if (startSound != null)
            platformAudio.PlayOneShot(startSound);

        if (riseLoop == null) return;

        platformAudio.clip = riseLoop;
        platformAudio.loop = true;
        platformAudio.Play();
    }

    void stopRiseLoop()
    {
        if (platformAudio == null || !platformAudio.loop) return;
        platformAudio.Stop();
        platformAudio.loop = false;
    }

    public bool IsSolved => _solved;
}