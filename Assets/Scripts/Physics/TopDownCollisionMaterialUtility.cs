using UnityEngine;

public static class TopDownCollisionMaterialUtility
{
    private static PhysicsMaterial2D noFrictionMaterial;

    public static PhysicsMaterial2D NoFrictionMaterial
    {
        get
        {
            if (noFrictionMaterial == null)
            {
                noFrictionMaterial = new PhysicsMaterial2D("TopDownNoFriction")
                {
                    friction = 0f,
                    bounciness = 0f
                };
                noFrictionMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            return noFrictionMaterial;
        }
    }

    public static void ApplyNoFriction(Collider2D collider)
    {
        if (collider == null || !Application.isPlaying)
        {
            return;
        }

        collider.sharedMaterial = NoFrictionMaterial;
    }
}
