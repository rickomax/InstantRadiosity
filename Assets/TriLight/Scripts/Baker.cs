using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TriLight
{
    /// <summary>
    /// Represents a struct that contains extra information about the raycast hit. (Color and bounce index)
    /// </summary>
    public struct RaycastHitExtra
    {
        public Color Color;
        public int Bounce;
    }

    /// <summary>
    /// Represents a class that casts rays from a light source and creates spot lights on the hitting points.
    /// </summary>
    public class Baker : MonoBehaviour
    {
        /// <summary>
        /// The number of initial rays to dispatch per light.
        /// </summary>
        public int RaysPerLight = 1000;

        /// <summary>
        /// The maximum number of ray bounces.
        /// </summary>
        public int MaxBounces = 3;

        /// <summary>
        /// The created spot light radius. (Some bounding sphere radius could be used here)
        /// </summary>
        public float LightRadius = 300f;

        /// <summary>
        /// The distance in the direction of the hit normal to create the spot light.
        /// </summary>
        public float LightBleeding = 0.01f;

        /// <summary>
        /// The rays layer mask.
        /// </summary>
        public LayerMask LayerMask;

        /// <summary>
        /// The maximum ray distance.
        /// </summary>
        public float MaxRayDistance = 10000f;

        private IList<Light> _lights;
        private NativeArray<RaycastHit> _raycastHits;
        private NativeArray<RaycastCommand> _raycastCommands;
        private NativeArray<RaycastHitExtra> _raycastHitExtras;

        /// <summary>
        /// Gathers all scene lights, creates internal lists and dispatches the rays.
        /// </summary>
        private void Start()
        {
            UpdateLights();
            _raycastHits = new NativeArray<RaycastHit>(RaysPerLight, Allocator.Persistent);
            _raycastCommands = new NativeArray<RaycastCommand>(RaysPerLight, Allocator.Persistent);
            _raycastHitExtras = new NativeArray<RaycastHitExtra>(RaysPerLight, Allocator.Persistent);
            foreach (var light in _lights)
            {
                for (var bounceIndex = 0; bounceIndex < MaxBounces; bounceIndex++)
                {
                    TraceLight(light, bounceIndex);
                }
            }
        }

        /// <summary>
        /// Traces a ray in the given light direction.
        /// </summary>
        /// <param name="rayLight">The light to emit the rays.</param>
        /// <param name="bounceIndex">The bounce index.</param>
        private void TraceLight(Light rayLight, int bounceIndex)
        {
            for (var rayIndex = 0; rayIndex < RaysPerLight; rayIndex++)
            {
                var raycastHitExtra = _raycastHitExtras[rayIndex];
                raycastHitExtra.Color = rayLight.color;
                Vector3 position;
                Vector3 direction;
                if (bounceIndex == 0)
                {
                    position = -rayLight.transform.forward * LightRadius * 2f;
                    position += Random.onUnitSphere * LightRadius * 0.5f;
                    if (Vector3.Dot(position, rayLight.transform.forward) > 0.0f)
                    {
                        position = -position;
                    }
                    direction = rayLight.transform.forward;
                    // Debug.DrawRay(position, direction * 100f, Color.white, 10f);
                }
                else
                {
                    if (_raycastHits[rayIndex].collider == null)
                    {
                        continue;
                    }
                    position = _raycastHits[rayIndex].point;
                    direction = Random.onUnitSphere;
                    if (Vector3.Dot(direction, _raycastHits[rayIndex].normal) <= 0.0f)
                    {
                        direction = -direction;
                    }
                    //Debug.DrawRay(position, direction * 100f, Color.red, 10f);
                }
                raycastHitExtra.Bounce = bounceIndex;
                _raycastHitExtras[rayIndex] = raycastHitExtra;
                _raycastCommands[rayIndex] = new RaycastCommand(position, direction, MaxRayDistance, LayerMask, 1);
            }
            var jobHandle = RaycastCommand.ScheduleBatch(_raycastCommands, _raycastHits, 32);
            jobHandle.Complete();
            for (var rayIndex = 0; rayIndex < _raycastHits.Length; rayIndex++)
            {
                var raycastHit = _raycastHits[rayIndex];
                if (raycastHit.collider == null)
                {
                    continue;
                }
                if (raycastHit.collider.TryGetComponent<Renderer>(out var raycastHitRenderer))
                {
                    if (raycastHitRenderer.material != null)
                    {
                        var raycastHitExtra = _raycastHitExtras[rayIndex];
                        raycastHitExtra.Color *= raycastHitRenderer.material.color;
                        _raycastHitExtras[rayIndex] = raycastHitExtra;
                    }
                }
                CreateLight(rayIndex, raycastHit.collider.transform);
            }
        }

        /// <summary>
        /// Creates a light at the raycast result at the given index position.
        /// </summary>
        /// <param name="index">The raycast result index.</param>
        /// <param name="parent">The raycast hit collider transform.</param>
        private void CreateLight(int index, Transform parent)
        {
            var raycastHit = _raycastHits[index];
            var raycastHitExtra = _raycastHitExtras[index];
            var lightGameObject = new GameObject("Virtual Light");
            var light = lightGameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.transform.forward = raycastHit.normal;
            light.range = LightRadius;
            light.color = raycastHitExtra.Color;
            light.intensity = 0.014f;
            light.shadows = LightShadows.Soft;
            light.shadowNearPlane = 30f;
            light.transform.position = raycastHit.point + raycastHit.normal * LightBleeding;
            light.transform.parent = parent;
        }

        /// <summary>
        /// Destroys the native arrays.
        /// </summary>
        private void OnDestroy()
        {
            _raycastHits.Dispose();
            _raycastCommands.Dispose();
            _raycastHitExtras.Dispose();
        }

        /// <summary>
        /// Gathers all scene lights.
        /// </summary>
        private void UpdateLights()
        {
            _lights = FindObjectsOfType<Light>(false);
        }
    }
}