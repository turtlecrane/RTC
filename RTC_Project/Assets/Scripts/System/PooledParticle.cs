using System.Collections;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class PooledParticle : MonoBehaviour
{
    public string PoolKey; // 이 인스턴스가 속한 풀의 키 (ParticlePool에서 초기화해줌)

    private ParticleSystem ps;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
    }

    private void OnEnable()
    {
        // 재생 시작 시 자동으로 끝날 때까지 대기했다가 풀에 반납
        StartCoroutine(DisableWhenFinished());
    }

    private IEnumerator DisableWhenFinished()
    {
        // 안전하게 파티클 길이 계산: duration + startLifetime(최대)
        var main = ps.main;
        float lifetime = main.duration;

        // startLifetime은 MinMaxCurve일 수 있으니 최대값을 추출
        if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
        {
            lifetime += main.startLifetime.constant;
        }
        else
        {
            // 대략적인 안전 마진을 위해 두 배로
            lifetime += (main.startLifetime.constantMax > 0 ? main.startLifetime.constantMax : main.startLifetime.constant);
        }

        // 루프 파티클인 경우는 반납하지 않음 (무한 재생). 필요하면 별도 처리.
        if (main.loop)
            yield break;

        // Wait for the duration (plus a tiny buffer)
        yield return new WaitForSeconds(lifetime + 0.05f);

        // 반납
        if (ParticlePool.Instance != null)
        {
            ParticlePool.Instance.ReturnToPool(PoolKey, gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}