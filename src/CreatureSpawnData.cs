using RainMeadow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureThePearl;

public class CreatureSpawnData : OnlineEntity.EntityData
{
    public int spawner;
    public WorldCoordinate spawnDen;
    public CreatureSpawnData() : base() { }
    public CreatureSpawnData(AbstractCreature ac) : base()
    {
        spawner = ac.ID.spawner;
        spawnDen = ac.spawnDen;
    }

    public override EntityDataState MakeState(OnlineEntity entity, OnlineResource inResource)
    {
        return new State(this);
    }

    private class State : EntityDataState
    {
        public override Type GetDataType() => typeof(CreatureSpawnData);

        [OnlineField]
        private int spawner;
        [OnlineField]
        private WorldCoordinate spawnDen;

        public State() : base() { }
        public State(CreatureSpawnData data)
        {
            spawner = data.spawner;
            spawnDen = data.spawnDen;
        }

        public override void ReadTo(OnlineEntity.EntityData data, OnlineEntity onlineEntity)
        {
            if (onlineEntity is OnlineCreature oc)
            {
                oc.abstractCreature.ID.spawner = spawner;
                oc.abstractCreature.spawnDen = spawnDen;
            }
        }
    }
}
