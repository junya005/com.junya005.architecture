using UnityEngine;

/// <summary>
/// これを継承したコンポーネントはシングルトンパターンが適用されます
/// </summary>
/// <typeparam name="TComponent">シングルトンパターンを適用するコンポーネント型</typeparam>
public abstract class SingletonMonoBehaviour<TComponent> : MonoBehaviour
    where TComponent : SingletonMonoBehaviour<TComponent>
{
    [Header("Singleton Settings")]
    [SerializeField, Tooltip("シーン遷移時に破棄されないようにするか")]
    private bool _dontDestroyOnLoad = true;

    private static TComponent _instance;

    /// <summary>
    /// シングルトンインスタンスが既に存在するかどうか
    /// （存在しない場合に新規生成を行わずに確認したい場合に使用）
    /// </summary>
    public static bool HasInstance => _instance != null;

    /// <summary>
    /// 取得時に自動でインスタンスを生成するか
    /// </summary>
    protected static bool AutoCreateInstance = true;

    /// <summary>
    /// シングルトンインスタンスを取得します。
    /// 存在しない場合はシーン内を検索し、それでも見つからなければ自動生成します。
    /// </summary>
    /// <remarks>
    /// 存在しない場合の自動生成を実行するかはAutoCreateInstanceを用いて設定可能です
    /// </remarks>
    public static TComponent Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<TComponent>();

                if (_instance == null)
                {
                    if (!AutoCreateInstance) // ← ここで判定
                    {
                        Debug.LogError($"[{typeof(TComponent).Name}] インスタンスがシーン内に見つかりません。自動生成が無効化されているため、シーンまたはプレハブに配置してください。");
                        return null;
                    }

                    var go = new GameObject(typeof(TComponent).Name);
                    _instance = go.AddComponent<TComponent>();
                }
            }

            return _instance;
        }
    }

    protected virtual void Awake()
    {
        // 既に別のインスタンスが存在する場合は重複とみなして自身を破棄
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[{typeof(TComponent).Name}] 既にインスタンスが存在するため、重複するオブジェクト '{gameObject.name}' を破棄します。");
            Destroy(gameObject);
            return;
        }

        _instance = this as TComponent;

        if (_dontDestroyOnLoad)
        {
            // 親が存在する場合はDontDestroyOnLoadで警告が出るためルートに移動
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }

            DontDestroyOnLoad(gameObject);
        }

        OnAwake();
    }

    /// <summary>
    /// 派生クラス用の初期化処理（Awake相当）
    /// </summary>
    protected virtual void OnAwake()
    {
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }

        OnDestroyed();
    }

    /// <summary>
    /// 派生クラス用の破棄時処理（OnDestroy相当）
    /// </summary>
    protected virtual void OnDestroyed()
    {
    }
}
