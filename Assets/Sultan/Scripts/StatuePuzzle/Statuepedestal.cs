using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class StatuePedestal : MonoBehaviour
{
    public enum Animal { None, Chicken, Wolf, Elephant }

    [SerializeField] private Transform flapA;
    [SerializeField] private Transform flapB;
    [SerializeField] private Transform animalAnchor;
    [SerializeField] private GameObject chickenVisual;
    [SerializeField] private GameObject wolfVisual;
    [SerializeField] private GameObject elephantVisual;
    [SerializeField] private GameObject pedestalLight;
    [SerializeField] private float rotationValue = 25f;
    [SerializeField] private Vector3 flapAxis = Vector3.forward;
    [SerializeField] private float flapOpenAngleA = 90f;
    [SerializeField] private float flapOpenAngleB = -90f;
    [SerializeField] private float hideDepth = 0.8f;
    [SerializeField] private Animal startingAnimal = Animal.None;
    [SerializeField] private float flapOpenDuration = 0.35f;
    [SerializeField] private float lowerDuration = 0.4f;
    [SerializeField] private float swapPause = 0.08f;
    [SerializeField] private float raiseDuration = 0.5f;
    [SerializeField] private float flapCloseDuration = 0.35f;
    [SerializeField] private AnimationCurve flapCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve liftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool debug;

    public event Action<StatuePedestal> OnAnimalChanged;

    private static readonly List<StatuePedestal> _all = new List<StatuePedestal>();
    private static readonly Animal[] _order = { Animal.Chicken, Animal.Wolf, Animal.Elephant };

    private readonly List<Animal> _ring = new List<Animal>(4);
    private Animal _held = Animal.None;
    private Animal _visible = Animal.None;
    private Quaternion _flapAClosed;
    private Quaternion _flapBClosed;
    private Quaternion _flapAOpen;
    private Quaternion _flapBOpen;
    private Vector3 _raisedPos;
    private Vector3 _hiddenPos;
    private bool _busy;
    private bool _locked;

    void Awake()
    {
        if (flapA != null)
        {
            _flapAClosed = flapA.localRotation;
            _flapAOpen = Quaternion.AngleAxis(flapOpenAngleA, flapAxis) * _flapAClosed;
        }

        if (flapB != null)
        {
            _flapBClosed = flapB.localRotation;
            _flapBOpen = Quaternion.AngleAxis(flapOpenAngleB, flapAxis) * _flapBClosed;
        }

        if (animalAnchor != null)
        {
            _raisedPos = animalAnchor.localPosition;
            _hiddenPos = _raisedPos + Vector3.down * hideDepth;
        }

        _held = startingAnimal;
        _visible = startingAnimal;
        snapToRest();
    }

    void OnEnable()
    {
        if (!_all.Contains(this)) _all.Add(this);
    }

    void OnDisable()
    {
        _all.Remove(this);
        StopAllCoroutines();
        _busy = false;
        _visible = _held;
        snapToRest();
    }

    public bool Cycle()
    {
        if (_busy || _locked) return false;

        Animal next = nextAnimal();
        if (next == _held) return false;

        Animal previous = _held;
        _held = next;

        if (debug)
            Debug.Log($"[Pedestal] {name} {previous} -> {_held} | value {rotationValue}");

        OnAnimalChanged?.Invoke(this);
        StartCoroutine(sequence());
        return true;
    }

    Animal nextAnimal()
    {
        _ring.Clear();
        _ring.Add(Animal.None);

        for (int i = 0; i < _order.Length; i++)
            if (isFree(_order[i])) _ring.Add(_order[i]);

        int index = _ring.IndexOf(_held);
        if (index < 0) return Animal.None;

        return _ring[(index + 1) % _ring.Count];
    }

    bool isFree(Animal a)
    {
        for (int i = 0; i < _all.Count; i++)
        {
            if (_all[i] == this) continue;
            if (_all[i]._held == a) return false;
        }
        return true;
    }

    IEnumerator sequence()
    {
        _busy = true;

        yield return rotateFlaps(_flapAOpen, _flapBOpen, flapOpenDuration);

        if (_visible != Animal.None)
        {
            yield return moveAnchor(_hiddenPos, lowerDuration);
            _visible = Animal.None;
            showOnly(_visible);
            setLight(_held != Animal.None);

            if (swapPause > 0f)
                yield return new WaitForSeconds(swapPause);
        }

        if (_held != Animal.None)
        {
            if (animalAnchor != null) animalAnchor.localPosition = _hiddenPos;
            _visible = _held;
            showOnly(_visible);
            setLight(true);
            yield return moveAnchor(_raisedPos, raiseDuration);
        }

        yield return rotateFlaps(_flapAClosed, _flapBClosed, flapCloseDuration);

        _busy = false;
    }

    IEnumerator rotateFlaps(Quaternion targetA, Quaternion targetB, float duration)
    {
        Quaternion fromA = flapA != null ? flapA.localRotation : Quaternion.identity;
        Quaternion fromB = flapB != null ? flapB.localRotation : Quaternion.identity;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = flapCurve.Evaluate(Mathf.Clamp01(t / duration));
            if (flapA != null) flapA.localRotation = Quaternion.SlerpUnclamped(fromA, targetA, k);
            if (flapB != null) flapB.localRotation = Quaternion.SlerpUnclamped(fromB, targetB, k);
            yield return null;
        }

        if (flapA != null) flapA.localRotation = targetA;
        if (flapB != null) flapB.localRotation = targetB;
    }

    IEnumerator moveAnchor(Vector3 target, float duration)
    {
        if (animalAnchor == null) yield break;

        Vector3 from = animalAnchor.localPosition;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = liftCurve.Evaluate(Mathf.Clamp01(t / duration));
            animalAnchor.localPosition = Vector3.LerpUnclamped(from, target, k);
            yield return null;
        }

        animalAnchor.localPosition = target;
    }

    void snapToRest()
    {
        if (flapA != null) flapA.localRotation = _flapAClosed;
        if (flapB != null) flapB.localRotation = _flapBClosed;
        if (animalAnchor != null)
            animalAnchor.localPosition = _visible == Animal.None ? _hiddenPos : _raisedPos;
        showOnly(_visible);
        setLight(_visible != Animal.None);
    }

    void showOnly(Animal a)
    {
        if (chickenVisual != null) chickenVisual.SetActive(a == Animal.Chicken);
        if (wolfVisual != null) wolfVisual.SetActive(a == Animal.Wolf);
        if (elephantVisual != null) elephantVisual.SetActive(a == Animal.Elephant);
    }

    void setLight(bool state)
    {
        if (pedestalLight == null) return;
        if (pedestalLight.activeSelf == state) return;
        pedestalLight.SetActive(state);
    }

    [ContextMenu("Cycle")]
    void cycleFromMenu()
    {
        if (!Application.isPlaying) return;
        Cycle();
    }

    public void Lock() => _locked = true;
    public void Unlock() => _locked = false;

    public Animal Held => _held;
    public float RotationValue => rotationValue;
    public bool IsBusy => _busy;
    public bool IsLocked => _locked;
    public bool CanInteract => !_busy && !_locked;
}