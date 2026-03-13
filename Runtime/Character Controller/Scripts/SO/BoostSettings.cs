using UnityEngine;

/*
 * Boost settings container
 * by: YuukiDev
 *
 * ScriptableObject tuning data for boost capacity and regen.
 */
[CreateAssetMenu(menuName = "Flight/Boost Settings")]
public class BoostSettings : ScriptableObject
{
    public float capacity = 100f;
    public float drainRate = 20f;
    public float regenRate = 10f;
    public AnimationCurve regenCurve;
}
