using System;
using System.Reflection;

/// <summary>
/// MonoBehaviourを継承しない、純粋なC#クラス（POCO）向けのジェネリック・シングルトン基底クラスです。
/// スレッドセーフな遅延初期化、非公開コンストラクタによる単一性の保証、破棄・リセット機能を提供します。
/// </summary>
/// <typeparam name="T">シングルトンを適用する派生クラス型</typeparam>
public abstract class Singleton<T> where T : Singleton<T>
{
    private static readonly object _lock = new object();
    private static T _instance;

    /// <summary>
    /// シングルトンインスタンスを取得します。
    /// 初回アクセス時にスレッドセーフに遅延初期化されます。
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                // スレッドセーフのためのダブルチェックロッキング
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = CreateInstance();
                        _instance.OnInitialize();
                    }
                }
            }

            return _instance;
        }
    }

    /// <summary>
    /// インスタンスが既に生成されているかどうかを取得します。
    /// </summary>
    public static bool HasInstance
    {
        get
        {
            lock (_lock)
            {
                return _instance != null;
            }
        }
    }

    /// <summary>
    /// 外部からの直接インスタンス化（new）を防ぐためのコンストラクタ
    /// </summary>
    protected Singleton()
    {
    }

    /// <summary>
    /// 非公開（protected / private）の引数なしコンストラクタを呼び出してインスタンスを生成します。
    /// </summary>
    private static T CreateInstance()
    {
        var constructor = typeof(T).GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        if (constructor == null)
        {
            throw new InvalidOperationException(
                $"[{typeof(T).Name}] 引数なしのコンストラクタ（protected または private）が見つかりません。");
        }

        return (T)constructor.Invoke(null);
    }

    /// <summary>
    /// インスタンス生成直後に呼び出される初期化メソッド
    /// </summary>
    protected virtual void OnInitialize()
    {
    }

    /// <summary>
    /// インスタンスを破棄（リセット）します。
    /// クラスが IDisposable を実装している場合は Dispose() も呼び出されます。
    /// </summary>
    public static void DisposeInstance()
    {
        lock (_lock)
        {
            if (_instance is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _instance = null;
        }
    }
}
