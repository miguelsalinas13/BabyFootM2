using UnityEngine;

public sealed class BabyFootBallShadow : MonoBehaviour
{
    SpriteRenderer patch;
    Sprite sprite;
    Texture2D texture;
    void Awake()
    {
        const int size=64;
        texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
        texture.wrapMode=TextureWrapMode.Clamp;
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float radius=new Vector2(x-31.5f,y-31.5f).magnitude/31.5f;
            pixels[y*size+x]=new Color(0.07f,.06f,.045f,1-Mathf.SmoothStep(0,1,radius));
        }
        texture.SetPixels32(pixels);texture.Apply(false,true);
        sprite=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,1100);
        patch=new GameObject("Ball contact shadow").AddComponent<SpriteRenderer>();
        patch.transform.SetParent(transform.parent,false);
        patch.transform.localRotation=Quaternion.Euler(90,0,0);
        patch.sprite=sprite;
        LateUpdate();
    }
    void LateUpdate()
    {
        Vector3 p=transform.localPosition;
        float height=Mathf.Max(0,p.y-.775f);
        patch.color=new Color(1,1,1,.56f*Mathf.Clamp01(1-height/.17f));
        patch.transform.localScale=Vector3.one*(1+height*4);
        p.y=.775f-BabyFootFreeMatch.Radius+.0005f;
        patch.transform.localPosition=p;
    }
    void OnDisable(){if(patch)patch.enabled=false;}
    void OnEnable(){if(patch)patch.enabled=true;}
    void OnDestroy(){if(patch)Destroy(patch.gameObject);if(sprite)Destroy(sprite);if(texture)Destroy(texture);}
}
