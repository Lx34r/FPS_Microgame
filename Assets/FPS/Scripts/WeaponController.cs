using UnityEngine;

public enum WeaponShootType
{
    Manual,
    Automatic,
    Charge,
}
//序列化存储CrosshairData信息，否则无法显示在inspector上
[System.Serializable]
public struct CrosshairData
{
    [Tooltip("武器准星资源")]
    public Sprite crosshairSprite;
    [Tooltip("准星大小")]
    public int crosshairSize;
    [Tooltip("准星颜色")]
    public Color crosshairColor;
}

[RequireComponent(typeof(AudioSource))]
public class WeaponController : MonoBehaviour
{
    [Header("Information")]
    [Tooltip("武器名，用于UI界面")]
    public string weaponName;
    [Tooltip("武器图标，用于UI界面")]
    public Sprite weaponIcon;

    [Tooltip("默认武器准星信息")]
    public CrosshairData crosshairDataDefault;
    [Tooltip("瞄准时武器准星信息")]
    public CrosshairData crosshairDataTargetInSight;

    [Header("Internal References")]
    [Tooltip("武器root，不使用武器时停用该object")]
    public GameObject weaponRoot;
    [Tooltip("武器枪口，子弹发射点")]
    public Transform weaponMuzzle;

    [Header("Shoot parameters")]
    [Tooltip("武器射击类型：Manual手动|Automatic自动|Charge充能蓄力")]
    public WeaponShootType shootType;
    [Tooltip("子弹Prefab")]
    public ProjectileBase projectilePrefab;
    [Tooltip("射击间隔")]
    public float delayBetweenShots = 0.5f;
    [Tooltip("子弹扩散角度")]
    public float bulletSpreadAngle = 0f;
    [Tooltip("每次发射子弹数")]
    public int bulletsPerShot = 1;
    [Tooltip("武器后坐力")]
    [Range(0f, 2f)]
    public float recoilForce = 1;
    [Tooltip("瞄准时默认视野比例")]
    [Range(0f, 1f)]
    public float aimZoomRatio = 1f;
    [Tooltip("瞄准时武器偏移")]
    public Vector3 aimOffset;

    [Header("Ammo parameters")]
    [Tooltip("每秒填充弹药的速度")]
    public float ammoReloadRate = 1f;
    [Tooltip("停止开火x秒后开始恢复发射次数（换弹")]
    public float ammoReloadDelay = 2f;
    [Tooltip("最大弹药数")]
    public float maxAmmo = 8;

    [Header("Charging parameters (仅作用于充能武器)")]
    [Tooltip("达到最大充能时间")]
    public float maxChargeDuration = 2f;
    [Tooltip("Initial ammo used when starting to charge")]
    public float ammoUsedOnStartCharge = 1f;
    [Tooltip("Additional ammo used when charge reaches its maximum")]
    public float ammoUsageRateWhileCharging = 1f;

    [Header("Audio & Visual")]
    [Tooltip("枪口闪光Prefab")]
    public GameObject muzzleFlashPrefab;
    [Tooltip("射击声效")]
    public AudioClip shootSFX;
    [Tooltip("充能声效")]
    public AudioClip changeWeaponSFX;

    float m_CurrentAmmo;
    float m_LastTimeShot = Mathf.NegativeInfinity;
    float m_TimeBeginCharge;
    Vector3 m_LastMuzzlePosition;

    public GameObject owner { get; set; }
    public GameObject sourcePrefab { get; set; }
    public bool isCharging { get; private set; }
    public float currentAmmoRatio { get; private set; }
    public bool isWeaponActive { get; private set; }
    public bool isCooling { get; private set; }
    public float currentCharge { get; private set; }
    public Vector3 muzzleWorldVelocity { get; private set; }
    public float GetAmmoNeededToShoot() => (shootType != WeaponShootType.Charge ? 1 : ammoUsedOnStartCharge) / maxAmmo;

    AudioSource m_ShootAudioSource;

    void Awake()
    {
        m_CurrentAmmo = maxAmmo;
        m_LastMuzzlePosition = weaponMuzzle.position;

        m_ShootAudioSource = GetComponent<AudioSource>();
        DebugUtility.HandleErrorIfNullGetComponent<AudioSource, WeaponController>(m_ShootAudioSource, this, gameObject);
    }

    void Update()
    {
        UpdateAmmo();

        UpdateCharge();

        if (Time.deltaTime > 0)
        {
            muzzleWorldVelocity = (weaponMuzzle.position - m_LastMuzzlePosition) / Time.deltaTime;
            m_LastMuzzlePosition = weaponMuzzle.position;
        }
    }

    void UpdateAmmo()
    {
        if (m_LastTimeShot + ammoReloadDelay < Time.time && m_CurrentAmmo < maxAmmo && !isCharging)
        {
            // reloads weapon over time
            m_CurrentAmmo += ammoReloadRate * Time.deltaTime;

            // limits ammo to max value
            m_CurrentAmmo = Mathf.Clamp(m_CurrentAmmo, 0, maxAmmo);

            isCooling = true;
        }
        else
        {
            isCooling = false;
        }

        if (maxAmmo == Mathf.Infinity)
        {
            currentAmmoRatio = 1f;
        }
        else
        {
            currentAmmoRatio = m_CurrentAmmo / maxAmmo;
        }
    }

    void UpdateCharge()
    {
        if (isCharging)
        {
            if (currentCharge < 1f)
            {
                float chargeLeft = 1f - currentCharge;

                // Calculate how much charge ratio to add this frame
                float chargeAdded = 0f;
                if (maxChargeDuration <= 0f)
                {
                    chargeAdded = chargeLeft;
                }
                chargeAdded = (1f / maxChargeDuration) * Time.deltaTime;
                chargeAdded = Mathf.Clamp(chargeAdded, 0f, chargeLeft);

                // See if we can actually add this charge
                float ammoThisChargeWouldRequire = chargeAdded * ammoUsageRateWhileCharging;
                //if (ammoThisChargeWouldRequire <= m_CurrentAmmo)
                {
                    // Use ammo based on charge added
                    UseAmmo(ammoThisChargeWouldRequire);

                    // set current charge ratio
                    currentCharge = Mathf.Clamp01(currentCharge + chargeAdded);
                }
            }
        }
    }

    public void ShowWeapon(bool show)
    {
        weaponRoot.SetActive(show);

        if (show && changeWeaponSFX)
        {
            m_ShootAudioSource.PlayOneShot(changeWeaponSFX);
        }

        isWeaponActive = show;
    }

    public void UseAmmo(float amount)
    {
        m_CurrentAmmo = Mathf.Clamp(m_CurrentAmmo - amount, 0f, maxAmmo);
        m_LastTimeShot = Time.time;
    }

    public bool HandleShootInputs(bool inputDown, bool inputHeld, bool inputUp)
    {
        switch (shootType)
        {
            case WeaponShootType.Manual:
                if (inputDown)
                {
                    return TryShoot();
                }
                return false;

            case WeaponShootType.Automatic:
                if (inputHeld)
                {
                    return TryShoot();
                }
                return false;

            case WeaponShootType.Charge:
                if (inputHeld)
                {
                    TryBeginCharge();
                }
                if (inputUp)
                {
                    return TryReleaseCharge();
                }
                return false;

            default:
                return false;
        }
    }

    bool TryShoot()
    {
        if (m_CurrentAmmo >= 1f 
            && m_LastTimeShot + delayBetweenShots < Time.time)
        {
            HandleShoot();
            m_CurrentAmmo -= 1;

            return true;
        }

        return false;
    }

    bool TryBeginCharge()
    {
        if (!isCharging 
            && m_CurrentAmmo >= ammoUsedOnStartCharge 
            && m_LastTimeShot + delayBetweenShots < Time.time)
        {
            UseAmmo(ammoUsedOnStartCharge); 
            isCharging = true;

            return true;
        }

        return false;
    }

    bool TryReleaseCharge()
    {
        if (isCharging)
        {
            HandleShoot();

            currentCharge = 0f;
            isCharging = false;

            return true;
        }
        return false;
    }

    void HandleShoot()
    {
        // spawn all bullets with random direction
        for (int i = 0; i < bulletsPerShot; i++)
        {
            Vector3 shotDirection = GetShotDirectionWithinSpread(weaponMuzzle);
            ProjectileBase newProjectile = Instantiate(projectilePrefab, weaponMuzzle.position, Quaternion.LookRotation(shotDirection));
            newProjectile.Shoot(this);
        }

        // muzzle flash
        if (muzzleFlashPrefab != null)
        {
            GameObject muzzleFlashInstance = Instantiate(muzzleFlashPrefab, weaponMuzzle.position, weaponMuzzle.rotation, weaponMuzzle.transform);
            Destroy(muzzleFlashInstance, 2f);
        }

        m_LastTimeShot = Time.time;

        // play shoot SFX
        if (shootSFX)
        {
            m_ShootAudioSource.PlayOneShot(shootSFX);
        }
    }

    public Vector3 GetShotDirectionWithinSpread(Transform shootTransform)
    {
        float spreadAngleRatio = bulletSpreadAngle / 180f;
        Vector3 spreadWorldDirection = Vector3.Slerp(shootTransform.forward, UnityEngine.Random.insideUnitSphere, spreadAngleRatio);

        return spreadWorldDirection;
    }
}
