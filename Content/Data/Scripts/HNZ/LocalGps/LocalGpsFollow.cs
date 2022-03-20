using HNZ.Utils;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace HNZ.LocalGps
{
    public sealed class LocalGpsFollow
    {
        readonly IMyGps _gps;
        Vector3D _velocity;
        Vector3D _targetPosition;
        long _entityId;

        public LocalGpsFollow(IMyGps gps)
        {
            _gps = gps;
        }

        public void SetTargetPosition(Vector3D position)
        {
            _targetPosition = position;
        }

        public void SetTargetEntity(long entityId)
        {
            _entityId = entityId;
        }

        public void Update()
        {
            MyEntity entity;
            if (_entityId != 0 && MyEntities.TryGetEntityById(_entityId, out entity))
            {
                _gps.Coords = entity.PositionComp.GetPosition();
                return;
            }

            _gps.Coords = MathUtils.SmoothDamp(_gps.Coords, _targetPosition, ref _velocity, 1f, float.MaxValue, 0.016f);
        }
    }
}