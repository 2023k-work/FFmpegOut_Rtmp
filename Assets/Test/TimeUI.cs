using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class TimeUI : MonoBehaviour
{
    [FormerlySerializedAs("t")] public Text _t;

    void Start()
    {
        Application.targetFrameRate = 30;
    }

    private void Update()
    {
        _t.text = Time.time.ToString();
    }
}
