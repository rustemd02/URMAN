using Godot;

namespace Urman.Godot.Tests;

public partial class AddressWorldSmokeTest
{
    /// <summary>The last hop from a gate or door to its plate is walked around whatever stands in the
    /// yard instead of along a straight line through it: a standing-body search over 40 cm cells on
    /// the actual collision, then the controller follows the resulting waypoints with ordinary input.
    /// Returns null when the body finds no way, and the caller then fails with that fact.</summary>
    private async Task<List<Vector3>?> PlanYardLegAsync(Vector3 from,Vector3 to)
    {
        const float cell=.40f;const int limit=2500;
        using var probe=new AddressWalkProbe(_world);
        if(!probe.TrySupport(from,out var start))return null;
        var goalCell=((int)MathF.Round((to.X-start.X)/cell),(int)MathF.Round((to.Z-start.Z)/cell));
        var feet=new Dictionary<(int,int),Vector3>{[(0,0)]=start};
        var parent=new Dictionary<(int,int),(int,int)>();
        var cost=new Dictionary<(int,int),float>{[(0,0)]=0};
        var open=new PriorityQueue<(int,int),float>();open.Enqueue((0,0),0);
        var closed=new HashSet<(int,int)>();var expanded=0;(int,int)? reached=null;
        (int,int)[] moves=[(1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)];
        while(open.TryDequeue(out var node,out _)&&expanded<limit)
        {
            if(!closed.Add(node))continue;
            expanded++;
            var here=feet[node];
            if(new Vector2(here.X-to.X,here.Z-to.Z).Length()<=.25f){reached=node;break;}
            foreach(var (dx,dz) in moves)
            {
                var next=(node.Item1+dx,node.Item2+dz);
                if(closed.Contains(next))continue;
                var step=new Vector3(dx*cell,0,dz*cell);
                var position=here;var free=true;
                var parts=Math.Max(1,(int)MathF.Ceiling(step.Length()/.08f));
                for(var i=0;i<parts&&free;i++)free=probe.TryAdvance(position,step/parts,out position);
                if(!free)continue;
                var price=cost[node]+step.Length();
                if(cost.TryGetValue(next,out var known)&&known<=price)continue;
                cost[next]=price;feet[next]=position;parent[next]=node;
                var remaining=new Vector2(next.Item1-goalCell.Item1,next.Item2-goalCell.Item2).Length()*cell;
                open.Enqueue(next,price+remaining);
            }
            if(expanded%120==0)await Frames(1);
        }
        if(reached is null)return null;
        var path=new List<Vector3>();
        for((int,int)? at=reached;at is not null;at=parent.TryGetValue(at.Value,out var up)?up:null)path.Add(feet[at.Value]);
        path.Reverse();
        path.RemoveAt(0);
        path.Add(to);
        return path;
    }
}
