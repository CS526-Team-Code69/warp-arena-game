using UnityEngine;

namespace AnomalyArena
{

    public sealed class FxPool : MonoBehaviour
    {
        public Renderer[] items;
        public Texture ring, spark, sector;
        [Tooltip("V4 directional hit sparks; the texture points to +X and is rolled toward the hit direction on screen.")]
        public Texture[] hitSparks;
        public Texture hitCrit, hitRing;
        [Tooltip("Muzzle flash for the player's shots; the texture points to +X.")] public Texture muzzleFlash;
        public static FxPool Active { get; private set; }
        float[] lives, total;
        Vector3[] velocities;
        Color[] colors;
        float[] sizes;
        bool[] pulses;
        MaterialPropertyBlock block;
        void Awake(){Active=this;block=new MaterialPropertyBlock();lives=new float[items.Length];total=new float[items.Length];velocities=new Vector3[items.Length];colors=new Color[items.Length];sizes=new float[items.Length];pulses=new bool[items.Length];}
        public void Emit(Vector3 at,Color color,float size,float life,Vector3 velocity=default,Texture texture=null,Vector3 forward=default,bool flat=false,bool pulse=false,float roll=0f)
        {
            for(int i=0;i<items.Length;i++)
            {
                if(lives[i]>0)continue;var r=items[i];lives[i]=total[i]=Mathf.Max(.05f,life);velocities[i]=velocity;colors[i]=color;sizes[i]=size;pulses[i]=pulse||texture==ring||(!texture&&velocity.sqrMagnitude<.01f);
                r.gameObject.SetActive(true);r.transform.position=at+Vector3.up*.1f;r.transform.localScale=new Vector3(size,size,1);
                r.transform.rotation=flat?Quaternion.LookRotation(Vector3.up,forward.sqrMagnitude>0?forward:Vector3.forward):GameManager.Instance.cam.transform.rotation*Quaternion.Euler(0f,0f,roll);
                block.Clear();block.SetColor("_BaseColor",color);block.SetTexture("_BaseMap",texture?texture:spark);r.SetPropertyBlock(block);return;
            }
        }

        public void EmitDirectional(Texture texture,Vector3 at,Vector3 worldDirection,float size,float life,Color color)
        {
            var cam=GameManager.Instance?GameManager.Instance.cam:null;float roll=0f;
            if(cam&&worldDirection.sqrMagnitude>.0001f){var local=cam.transform.InverseTransformDirection(worldDirection);roll=Mathf.Atan2(local.y,local.x)*Mathf.Rad2Deg;}
            Emit(at,color,size,life,texture:texture,roll:roll);
        }
        void Update()
        {
            float dt=Time.unscaledDeltaTime;
            for(int i=0;i<items.Length;i++)
            {
                if(lives[i]<=0)continue;lives[i]-=dt;
                var item=items[i];
                if(lives[i]<=0){item.gameObject.SetActive(false);continue;}
                item.transform.position+=velocities[i]*dt;velocities[i]*=Mathf.Exp(-3f*dt);
                float progress=1-lives[i]/total[i];
                if(pulses[i]){float size=sizes[i]*(1+.65f*progress);item.transform.localScale=new Vector3(size,size,1);}
                item.GetPropertyBlock(block);var color=colors[i];color.a*=1-progress;block.SetColor("_BaseColor",color);item.SetPropertyBlock(block);
            }
        }
    }
}
