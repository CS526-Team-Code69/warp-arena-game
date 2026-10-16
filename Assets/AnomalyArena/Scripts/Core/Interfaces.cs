using UnityEngine;

namespace AnomalyArena
{
    public enum Team
    {
        Player,
        Enemy
    }

                                                                                                                             
    public enum CharacterState
    {
        Normal,               
        Knocked,                                                   
        Thrown,                                                                         
        Hooked,                                                                                          
        Dashing,                                                             
        Falling,                                                                                        
        Dead,
    }

    public enum KnockKind
    {
        Hit,                                                                         
        Throw,                                                     
        Push,                                         
    }

    public enum DeathCause
    {
        None,
        HpDepleted,
        FellIntoGap
    }

                                                                                                            
    public enum HitKind
    {
        Blunt,         
        Bullet,          
        Pierce,                                                               
        Slam,                                
        Blast,             
    }

    public struct DamageInfo
    {
        public float amount;

                                                                                                                                        
        public bool isAttack;

                                                                                                   
        public Vector3 knockDir;

        public float knockDistance;
        public IDamageDealer dealer;
        public HitKind kind;
        public DamageSource source;
        public ElementKind element;
        public float baseDamage;
        public bool ignoreShield;
        public bool builtInElement;
                                                                                  
        public HitFeel feel;

        public static DamageInfo Attack(float amount, IDamageDealer dealer, Vector3 knockDir = default,
            float knockDistance = 0f,
            HitKind kind = HitKind.Blunt) =>
            new DamageInfo
            {
                amount = amount, isAttack = true, dealer = dealer, knockDir = knockDir, knockDistance = knockDistance,
                kind = kind
            };
    }

                 
                                                                                                                      
                                                                                                                                    
                  
    public struct ThrowImpact
    {
                                                                                 
        public const float ReferenceRadius = 0.5f;

        public IDamageDealer dealer;

                                                                                                  
        public float damage;

                                                                                                                     
        public float sizeExponent;

                                                                                       
        public float radius;

                                                                                                                   
        public float knockback;
        public ElementKind element;
        public float stunSeconds;

        public float SizeFactor(float bodyRadius) => Mathf.Pow(bodyRadius / ReferenceRadius, sizeExponent);

                                                                                                                                       
        public float DamageAt(float speedRatio, float bodyRadius) =>
            Mathf.Max(1f, Mathf.Round(damage * SizeFactor(bodyRadius) * speedRatio * speedRatio));

                                                                                                                                      
        public float KnockbackAt(float speedRatio, float bodyRadius) => knockback * SizeFactor(bodyRadius) * speedRatio;
    }

    public interface IDamageDealer
    {
        Combatant Owner { get; }
    }

    public interface IDamageReceiver
    {
        bool IsAlive { get; }

                                                                                                    
        bool ReceiveDamage(DamageInfo info);
    }
}
