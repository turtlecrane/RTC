using System.Collections.Generic;
using UnityEngine;

public class ParticlePool : MonoBehaviour
{
    public static ParticlePool Instance { get; private set; }

    [System.Serializable]
    public class PoolItem
    {
        public string key;               // 호출할 때 사용할 이름 (ex: "MoveDust")
        public GameObject prefab;        // 파티클 프리팹
        public int initialSize = 10;     // 초기 풀 크기
        public bool expandable = true;   // 필요하면 풀 확장 허용
    }

    public List<PoolItem> pools = new List<PoolItem>();

    // 내부 저장소: key -> queue
    private Dictionary<string, Queue<GameObject>> poolDict = new Dictionary<string, Queue<GameObject>>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 초기화
        foreach (var p in pools)
        {
            var q = new Queue<GameObject>();
            for (int i = 0; i < p.initialSize; i++)
            {
                var go = Instantiate(p.prefab, transform);
                go.SetActive(false);
                // 붙여둔 PooledParticle가 있다면 PoolKey로 식별할 수 있게 해둠
                var pp = go.GetComponent<PooledParticle>();
                if (pp != null) pp.PoolKey = p.key;
                q.Enqueue(go);
            }
            poolDict[p.key] = q;
        }
    }

    // 사용: key로 파티클 요청
    public GameObject Get(string key, Vector3 position, Quaternion rotation)
    {
        if (!poolDict.ContainsKey(key))
        {
            Debug.LogWarning($"ParticlePool: No pool with key '{key}'");
            return null;
        }

        var queue = poolDict[key];
        GameObject item = null;

        // 큐에서 가져오되 비활성 오브젝트만 사용 (혹시 활성화된게 나올 경우 빈 슬롯확보)
        if (queue.Count > 0)
        {
            item = queue.Dequeue();
            if (item == null)
            {
                // 방어코드: null이면 재귀적으로 다시 시도
                return Get(key, position, rotation);
            }
        }

        // 만약 큐에서 꺼냈는데 활성화중이면 (안좋음) 또는 큐가 비어있으면 풀 확장 여부 체크
        if (item == null || item.activeSelf)
        {
            // Find PoolItem to check expandable
            var poolItem = pools.Find(x => x.key == key);
            if (poolItem != null && poolItem.expandable)
            {
                item = Instantiate(poolItem.prefab, transform);
                var pp = item.GetComponent<PooledParticle>();
                if (pp != null) pp.PoolKey = key;
            }
            else
            {
                Debug.LogWarning($"ParticlePool: Pool '{key}' exhausted and not expandable.");
                return null;
            }
        }

        item.transform.SetPositionAndRotation(position, rotation);
        item.SetActive(true);

        // PooledParticle가 자동으로 비활성화 시 풀에 다시 enqueue 하게 할 것이므로 여기서는 enqueue하지 않음.
        return item;
    }

    // PooledParticle이 호출해서 풀로 반납
    public void ReturnToPool(string key, GameObject obj)
    {
        if (!poolDict.ContainsKey(key))
        {
            // 만약 키가 등록되어있지 않다면 그냥 비활성화
            obj.SetActive(false);
            return;
        }

        obj.SetActive(false);
        poolDict[key].Enqueue(obj);
    }
}
