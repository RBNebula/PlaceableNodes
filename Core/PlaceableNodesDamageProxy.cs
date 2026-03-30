using UnityEngine;

namespace PlaceableNodes;

internal sealed class PlaceableNodesDamageProxy : MonoBehaviour, IDamageable
{
    private IDamageable? _target;

    internal void SetTarget(IDamageable target)
    {
        _target = target;
    }

    public void TakeDamage(float damage, Vector3 position)
    {
        IDamageable? target = _target;
        if (target == null || ReferenceEquals(target, this))
        {
            target = ResolveTargetFromParents();
            _target = target;
        }

        target?.TakeDamage(damage, position);
    }

    private IDamageable? ResolveTargetFromParents()
    {
        Transform? current = transform;
        while (current != null)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || ReferenceEquals(behaviour, this))
                {
                    continue;
                }

                if (behaviour is IDamageable damageable)
                {
                    return damageable;
                }
            }

            current = current.parent;
        }

        return null;
    }
}
