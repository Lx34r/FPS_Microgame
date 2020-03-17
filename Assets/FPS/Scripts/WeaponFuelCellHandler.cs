using UnityEngine;

[RequireComponent(typeof(WeaponController))]
public class WeaponFuelCellHandler : MonoBehaviour
{
    [Tooltip("武器燃料电池（子弹数）")]
    public GameObject[] fuelCells;
    [Tooltip("使用完电池位置（表现为弹出电池冷却）")]
    public Vector3 fuelCellUsedPosition;
    [Tooltip("使用前电池位置")]
    public Vector3 fuelCellUnusedPosition = new Vector3(0f, -0.1f, 0f);

    WeaponController m_Weapon;
    bool[] m_FuelCellsCooled;

    void Start()
    {
        m_Weapon = GetComponent<WeaponController>();
        DebugUtility.HandleErrorIfNullGetComponent<WeaponController, WeaponFuelCellHandler>(m_Weapon, this, gameObject);

        m_FuelCellsCooled = new bool[fuelCells.Length];
        for (int i = 0; i < m_FuelCellsCooled.Length; i++)
        {
            m_FuelCellsCooled[i] = true;
        }
    }

    void Update()
    {
        // TODO: needs simplification
        for (int i = 0; i < fuelCells.Length; i++)
        {
            float length = fuelCells.Length;
            float lim1 = i / length;
            float lim2 = (i + 1) / length;

            float value = Mathf.InverseLerp(lim1, lim2, m_Weapon.currentAmmoRatio);
            value = Mathf.Clamp01(value);

            fuelCells[i].transform.localPosition = Vector3.Lerp(fuelCellUsedPosition, fuelCellUnusedPosition, value);
        }
    }
}
