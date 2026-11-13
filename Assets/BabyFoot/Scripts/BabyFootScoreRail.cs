using UnityEngine;

// Each rail displays the current group of ten. The HUD retains the full match score.
public sealed class BabyFootScoreRail
{
    readonly Transform[,] beads=new Transform[2,10];
    readonly Vector3[,] home=new Vector3[2,10];
    readonly float[] stroke=new float[2];
    readonly float[,] from=new float[2,10];
    readonly float[] age=new float[2];
    readonly int[] count=new int[2];

    public BabyFootScoreRail(Transform table)
    {
        var mesh=Resources.Load<Mesh>("BabyFootScoreBead");
        if(!mesh)return;
        var renderers=table.GetComponentsInChildren<MeshRenderer>(true);
        foreach(var group in renderers)
        {
            if(!group.enabled || !group.gameObject.activeInHierarchy ||
                !group.name.StartsWith("Table_Score_Beads"))continue;
            var center=table.InverseTransformPoint(group.bounds.center);
            int team=center.z<0?0:1;
            if(beads[team,0])continue;
            var direction=team==0?1:-1;
            float spacing=mesh.bounds.size.x+.0015f;
            stroke[team]=direction*(.322f-spacing*10);
            for(int i=0;i<10;i++)
            {
                var bead=new GameObject("Score "+(team==0?"Blue ":"Coral ")+(i+1));
                bead.transform.SetParent(table,false);
                bead.AddComponent<MeshFilter>().sharedMesh=mesh;
                bead.AddComponent<MeshRenderer>().sharedMaterial=group.sharedMaterial;
                var position=center+Vector3.right*(direction*(spacing*(9.5f-i)-.161f));
                beads[team,i]=bead.transform;
                home[team,i]=position;
                bead.transform.localPosition=position;
            }
            group.enabled=false;
        }
    }

    public void SetScore(int blue,int coral,bool animate)
    {
        for(int team=0;team<2;team++)
        {
            if(!beads[team,0])continue;
            int score=team==0?blue:coral;
            int next=score==0?0:1+(score-1)%10;
            if(animate && count[team]==next)continue;
            for(int i=0;i<10;i++)from[team,i]=beads[team,i].localPosition.x;
            count[team]=next;
            age[team]=animate?0:.36f;
            Draw(team);
        }
    }

    public void Step(float dt)
    {
        for(int team=0;team<2;team++)
        {
            if(!beads[team,0] || age[team]>=.36f)continue;
            age[team]=Mathf.Min(.36f,age[team]+dt);
            Draw(team);
        }
    }

    void Draw(int team)
    {
        float t=Mathf.SmoothStep(0,1,age[team]/.36f);
        for(int i=0;i<10;i++)
        {
            var p=home[team,i];
            p.x=Mathf.Lerp(from[team,i],p.x+(i<count[team]?stroke[team]:0),t);
            beads[team,i].localPosition=p;
        }
    }
}
