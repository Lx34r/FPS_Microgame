using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ProjectileBase))]
public class ProjectileStandard : MonoBehaviour
{
    [Header("General")]
    [Tooltip("子弹碰撞检测半径")]
    public float radius = 0.01f;
    [Tooltip("子弹root（用于精确碰撞检测）")]
    public Transform root;
    [Tooltip("子弹tip（用于精确碰撞检测）")]
    public Transform tip;
    [Tooltip("子弹生命周期")]
    public float maxLifeTime = 5f;
    [Tooltip("子弹爆炸特效")]
    public GameObject impactVFX;
    [Tooltip("爆炸特效持续时间")]
    public float impactVFXLifetime = 5f;
    [Tooltip("爆炸特效生成位置偏移")]
    public float impactVFXSpawnOffset = 0.1f;
    [Tooltip("爆炸声效")]
    public AudioClip impactSFXClip;
    [Tooltip("子弹可以碰撞的Layers")]
    public LayerMask hittableLayers = -1;

    [Header("Movement")]
    [Tooltip("子弹速度")]
    public float speed = 20f;
    [Tooltip("重力向下加速度(子弹下坠)")]
    public float gravityDownAcceleration = 0f;
    [Tooltip("修正子弹轨迹以符合预定弹道(用于第一人称中修正子弹向屏幕中心偏移)，小于0不生效")]
    public float trajectoryCorrectionDistance = -1;
    [Tooltip("子弹是否继承开枪时枪口初速度")]
    public bool inheritWeaponVelocity = false;

    [Header("Damage")]
    [Tooltip("子弹伤害")]
    public float damage = 40f;
    [Tooltip("范围性伤害（None则命中后不会造成范围性伤害）")]
    public DamageArea areaOfDamage;

    [Header("Debug")]
    [Tooltip("debug时子弹半径颜色")]
    public Color radiusColor = Color.cyan * 0.2f;

    ProjectileBase m_ProjectileBase;
    Vector3 m_LastRootPosition;
    Vector3 m_Velocity;
    bool m_HasTrajectoryOverride;
    float m_ShootTime;
    Vector3 m_TrajectoryCorrectionVector;
    Vector3 m_ConsumedTrajectoryCorrectionVector;
    List<Collider> m_IgnoredColliders;

    const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;

    private void OnEnable()
    {
        m_ProjectileBase = GetComponent<ProjectileBase>();
        DebugUtility.HandleErrorIfNullGetComponent<ProjectileBase, ProjectileStandard>(m_ProjectileBase, this, gameObject);

        m_ProjectileBase.onShoot += OnShoot;

        Destroy(gameObject, maxLifeTime);
    }

    void OnShoot()
    {
        m_ShootTime = Time.time;
        m_LastRootPosition = root.position;
        m_Velocity = transform.forward * speed;
        m_IgnoredColliders = new List<Collider>();
        transform.position += m_ProjectileBase.inheritedMuzzleVelocity * Time.deltaTime;

        // Ignore colliders of owner
        Collider[] ownerColliders = m_ProjectileBase.owner.GetComponentsInChildren<Collider>();
        m_IgnoredColliders.AddRange(ownerColliders);

        // Handle case of player shooting (make projectiles not go through walls, and remember center-of-screen trajectory)
        PlayerWeaponsManager playerWeaponsManager = m_ProjectileBase.owner.GetComponent<PlayerWeaponsManager>();
        if(playerWeaponsManager)
        {
            m_HasTrajectoryOverride = true;

            Vector3 cameraToMuzzle = (m_ProjectileBase.initialPosition - playerWeaponsManager.weaponCamera.transform.position);

            m_TrajectoryCorrectionVector = Vector3.ProjectOnPlane(-cameraToMuzzle, playerWeaponsManager.weaponCamera.transform.forward);
            if (trajectoryCorrectionDistance == 0)
            {
                transform.position += m_TrajectoryCorrectionVector;
                m_ConsumedTrajectoryCorrectionVector = m_TrajectoryCorrectionVector;
            }
            else if (trajectoryCorrectionDistance < 0)
            {
                m_HasTrajectoryOverride = false;
            }
            
            if (Physics.Raycast(playerWeaponsManager.weaponCamera.transform.position, cameraToMuzzle.normalized, out RaycastHit hit, cameraToMuzzle.magnitude, hittableLayers, k_TriggerInteraction))
            {
                if (IsHitValid(hit))
                {
                    OnHit(hit.point, hit.normal, hit.collider);
                }
            }
        }
    }

    void Update()
    {
        // Move
        transform.position += m_Velocity * Time.deltaTime;
        if (inheritWeaponVelocity)
        {
            transform.position += m_ProjectileBase.inheritedMuzzleVelocity * Time.deltaTime;
        }

        // Drift towards trajectory override (this is so that projectiles can be centered 
        // with the camera center even though the actual weapon is offset)
        if (m_HasTrajectoryOverride && m_ConsumedTrajectoryCorrectionVector.sqrMagnitude < m_TrajectoryCorrectionVector.sqrMagnitude)
        {
            Vector3 correctionLeft = m_TrajectoryCorrectionVector - m_ConsumedTrajectoryCorrectionVector;
            float distanceThisFrame = (root.position - m_LastRootPosition).magnitude;
            Vector3 correctionThisFrame = (distanceThisFrame / trajectoryCorrectionDistance) * m_TrajectoryCorrectionVector;
            correctionThisFrame = Vector3.ClampMagnitude(correctionThisFrame, correctionLeft.magnitude);
            m_ConsumedTrajectoryCorrectionVector += correctionThisFrame;

            // Detect end of correction
            if(m_ConsumedTrajectoryCorrectionVector.sqrMagnitude == m_TrajectoryCorrectionVector.sqrMagnitude)
            {
                m_HasTrajectoryOverride = false;
            }

            transform.position += correctionThisFrame;
        }

        // Orient towards velocity
        transform.forward = m_Velocity.normalized;

        // Gravity
        if (gravityDownAcceleration > 0)
        {
            // add gravity to the projectile velocity for ballistic effect
            m_Velocity += Vector3.down * gravityDownAcceleration * Time.deltaTime;
        }

        // Hit detection
        {
            RaycastHit closestHit = new RaycastHit();
            closestHit.distance = Mathf.Infinity;
            bool foundHit = false;

            // Sphere cast
            Vector3 displacementSinceLastFrame = tip.position - m_LastRootPosition;
            RaycastHit[] hits = Physics.SphereCastAll(m_LastRootPosition, radius, displacementSinceLastFrame.normalized, displacementSinceLastFrame.magnitude, hittableLayers, k_TriggerInteraction);
            foreach (var hit in hits)
            {
                if (IsHitValid(hit) && hit.distance < closestHit.distance)
                {
                    foundHit = true;
                    closestHit = hit;
                }
            }

            if (foundHit)
            {
                // Handle case of casting while already inside a collider
                if(closestHit.distance <= 0f)
                {
                    closestHit.point = root.position;
                    closestHit.normal = -transform.forward;
                }

                OnHit(closestHit.point, closestHit.normal, closestHit.collider);
            }
        }

        m_LastRootPosition = root.position;
    }

    bool IsHitValid(RaycastHit hit)
    {
        // ignore hits with an ignore component
        if(hit.collider.GetComponent<IgnoreHitDetection>())
        {
            return false;
        }

        // ignore hits with triggers that don't have a Damageable component
        if(hit.collider.isTrigger && hit.collider.GetComponent<Damageable>() == null)
        {
            return false;
        }

        // ignore hits with specific ignored colliders (self colliders, by default)
        if (m_IgnoredColliders.Contains(hit.collider))
        {
            return false;
        }

        return true;
    }

    void OnHit(Vector3 point, Vector3 normal, Collider collider)
    { 
        // damage
        if (areaOfDamage)
        {
            // area damage
            areaOfDamage.InflictDamageInArea(damage, point, hittableLayers, k_TriggerInteraction, m_ProjectileBase.owner);
        }
        else
        {
            // point damage
            Damageable damageable = collider.GetComponent<Damageable>();
            if (damageable)
            {
                damageable.InflictDamage(damage, false, m_ProjectileBase.owner);
            }
        }

        // impact vfx
        if (impactVFX)
        {
            GameObject impactVFXInstance = Instantiate(impactVFX, point + (normal * impactVFXSpawnOffset), Quaternion.LookRotation(normal));
            if (impactVFXLifetime > 0)
            {
                Destroy(impactVFXInstance.gameObject, impactVFXLifetime);
            }
        }

        // impact sfx
        if (impactSFXClip)
        {
            AudioUtility.CreateSFX(impactSFXClip, point, AudioUtility.AudioGroups.Impact, 1f, 3f);
        }

        // Self Destruct
        Destroy(this.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = radiusColor;
        Gizmos.DrawSphere(transform.position, radius);
    }
}