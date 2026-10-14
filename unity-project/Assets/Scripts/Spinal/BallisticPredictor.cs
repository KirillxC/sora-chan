using System;
using UnityEngine;

namespace AiChan.Spinal
{
    public class BallisticPredictor : MonoBehaviour
    {
        [Header("Catch Parameters")]
        public float catchRadius = 0.75f; // Радиус досягаемости рук от груди
        public Transform chestTransform;

        [Header("Predicted Intercept")]
        public bool isBallIncoming = false;
        public Vector3 predictedInterceptPoint;
        public float timeToIntercept = 0f;

        public event Action<Vector3, float> OnBallIncoming;

        public bool PredictTrajectory(Vector3 startPos, Vector3 initialVelocity, Vector3 gravity, out Vector3 interceptPos, out float impactTime)
        {
            interceptPos = Vector3.zero;
            impactTime = 0f;

            if (chestTransform == null) chestTransform = transform;
            Vector3 targetCenter = chestTransform.position;

            // Дискретно-аналитический поиск ближайшей точки параболы к груди
            float dt = 0.02f;
            float maxTime = 2.0f;
            float minDistance = float.MaxValue;
            float bestTime = -1f;
            Vector3 bestPos = Vector3.zero;

            for (float t = 0f; t <= maxTime; t += dt)
            {
                Vector3 posAtT = startPos + initialVelocity * t + 0.5f * gravity * (t * t);
                float dist = Vector3.Distance(posAtT, targetCenter);

                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestTime = t;
                    bestPos = posAtT;
                }

                // Мяч упал на пол
                if (posAtT.y <= 0.05f) break;
            }

            if (minDistance <= catchRadius && bestTime > 0.05f)
            {
                interceptPos = bestPos;
                impactTime = bestTime;
                isBallIncoming = true;
                predictedInterceptPoint = interceptPos;
                timeToIntercept = impactTime;
                OnBallIncoming?.Invoke(interceptPos, impactTime);
                return true;
            }

            isBallIncoming = false;
            return false;
        }

        /// <summary>
        /// Расчет скорости броска мяча в заданную цель (кольцо или руки игрока)
        /// </summary>
        public static Vector3 CalculateThrowVelocity(Vector3 startPos, Vector3 targetPos, float flightTime, Vector3 gravity)
        {
            if (flightTime <= 0.01f) flightTime = 0.5f;
            // V0 = (P_target - P_start - 0.5 * g * t^2) / t
            Vector3 displacement = targetPos - startPos;
            Vector3 gravityOffset = 0.5f * gravity * (flightTime * flightTime);
            return (displacement - gravityOffset) / flightTime;
        }
    }
}
