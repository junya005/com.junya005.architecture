#nullable enable

using System;
using System.Collections.Generic;

namespace Junya005.Architecture
{
    /// <summary>
    /// HFSMの状態を管理するメソッドを提供するクラス
    /// </summary>
    /// <typeparam name="TContext">状態機械で使いまわすコンテキスト</typeparam>
    /// <typeparam name="TEvent">状態機械で使いまわすイベント</typeparam>
    public sealed class HierarchicalStateMachine<TContext, TEvent> : IEventSink<TEvent>
        where TEvent : notnull
    {
        private readonly TContext _context;
        private readonly State<TContext, TEvent> _root;

        // どちらも状態マシンの生存期間中、再利用する。
        private readonly List<State<TContext, TEvent>> _activePath;
        private readonly List<State<TContext, TEvent>> _targetPath;

        private bool _isExecuting;

        public bool IsStarted { get; private set; }

        public State<TContext, TEvent>? CurrentLeaf =>
            _activePath.Count == 0
                ? null
                : _activePath[_activePath.Count - 1];

        /// <summary>
        /// HFSM機構を初期化します
        /// </summary>
        /// <param name="context">HFSMで使いまわすContext</param>
        /// <param name="root">HFSMのルートとなる状態param>
        /// <param name="initialDepthCapacity">ルートを含む経路の長さ</param>
        /// <exception cref="ArgumentNullException">rootの値が空の場合に出力</exception>
        /// <exception cref="ArgumentException">rootにParentが設定されている場合に出力</exception>
        /// <exception cref="ArgumentOutOfRangeException">initialDepthCapacityが0以下の場合に出力</exception>
        public HierarchicalStateMachine(
            TContext context,
            State<TContext, TEvent> root,
            int initialDepthCapacity = 8)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (root.Parent != null)
                throw new ArgumentException(
                    "ルート状態には親を設定できません。",
                    nameof(root));

            if (initialDepthCapacity < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(initialDepthCapacity));

            _context = context;
            _root = root;

            _activePath =
                new List<State<TContext, TEvent>>(initialDepthCapacity);

            _targetPath =
                new List<State<TContext, TEvent>>(initialDepthCapacity);
        }

        /// <summary>
        /// HFSM機構を開始します
        /// </summary>
        /// <exception cref="InvalidOperationException">すでに機構が開始している場合に出力</exception>
        public void Start()
        {
            BeginOperation();

            try
            {
                if (IsStarted)
                    throw new InvalidOperationException("開始済みです。");

                IsStarted = true;

                EnterState(_root);
                EnterInitialChildren();
            }
            finally
            {
                _isExecuting = false;
            }
        }

        /// <summary>
        /// HFSM機構を更新します。
        /// </summary>
        /// <remarks>
        /// Unityのライフサイクルでは、Updateから呼ばれることを想定しています。
        /// </remarks>
        /// <param name="deltaTime"></param>
        public void Tick(float deltaTime)
        {
            BeginOperation();

            try
            {
                EnsureStarted();

                // 親から子へ更新する。
                int count = _activePath.Count;

                for (int i = 0; i < count; i++)
                    _activePath[i].OnTick(_context, deltaTime);
            }
            finally
            {
                _isExecuting = false;
            }
        }

        /// <summary>
        /// イベントから遷移できる状態を探して遷移します
        /// </summary>
        /// <param name="input">判断してほしいイベント</param>
        /// <returns>遷移したか</returns>
        public bool Dispatch(TEvent input)
        {
            BeginOperation();

            try
            {
                EnsureStarted();

                // 末端の状態を優先し、未処理なら祖先へ進む。
                for (int i = _activePath.Count - 1; i >= 0; i--)
                {
                    var source = _activePath[i];

                    if (!source.TryGetTransition(input, out var target))
                        continue;

                    TransitionTo(target!);
                    return true;
                }

                return false;
            }
            finally
            {
                _isExecuting = false;
            }
        }

        /// <summary>
        /// 特定の状態がアクティブか調査します
        /// </summary>
        /// <param name="state">調査する状態</param>
        /// <returns>アクティブかどうか</returns>
        public bool IsActive(State<TContext, TEvent> state)
        {
            foreach (var active in _activePath)
            {
                if (ReferenceEquals(active, state))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 指定された状態へ遷移します。
        /// </summary>
        /// <param name="target">遷移先の状態</param>
        private void TransitionTo(State<TContext, TEvent> target)
        {
            // Clearは要素を消すが、確保済みの容量は再利用する。
            _targetPath.Clear();

            try
            {
                // まず末端からルートへ積む。
                for (State<TContext, TEvent>? current = target;
                     current != null;
                     current = current.Parent)
                {
                    _targetPath.Add(current);
                }

                // ルートから遷移先への順序にする。
                _targetPath.Reverse();

                int commonLength = 0;

                while (commonLength < _activePath.Count &&
                       commonLength < _targetPath.Count &&
                       ReferenceEquals(
                           _activePath[commonLength],
                           _targetPath[commonLength]))
                {
                    commonLength++;
                }

                // 共通祖先より下を退出。
                while (_activePath.Count > commonLength)
                    ExitLeaf();

                // 遷移先へ進入。
                for (int i = commonLength; i < _targetPath.Count; i++)
                    EnterState(_targetPath[i]);

                EnterInitialChildren();
            }
            finally
            {
                // 作業用バッファに状態参照を残さない。
                _targetPath.Clear();
            }
        }

        /// <summary>
        /// 現在の状態階層の初期状態へ遷移します。
        /// </summary>
        private void EnterInitialChildren()
        {
            while (CurrentLeaf?.InitialChild is { } child)
                EnterState(child);
        }

        /// <summary>
        /// 指定した状態に入ります。
        /// </summary>
        /// <param name="state">遷移する状態</param>
        private void EnterState(State<TContext, TEvent> state)
        {
            _activePath.Add(state);
            state.OnEnter(_context);
        }

        /// <summary>
        /// 現在の状態を抜けます。
        /// </summary>
        private void ExitLeaf()
        {
            var state = _activePath[^1];

            state.OnExit(_context);
            _activePath.RemoveAt(_activePath.Count - 1);
        }

        /// <summary>
        /// HFSMの開始フラグを立てます
        /// </summary>
        /// <exception cref="InvalidOperationException">HFSMの開始フラグが立っていた場合に出力</exception>
        private void BeginOperation()
        {
            if (_isExecuting)
            {
                throw new InvalidOperationException("状態コールバックからの再入は禁止しています。");
            }

            _isExecuting = true;
        }

        /// <summary>
        /// HFSMが開始されたか調べます
        /// </summary>
        /// <exception cref="InvalidOperationException">開始されていない場合に出力</exception>
        private void EnsureStarted()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("先にStart()を呼んでください。");
            }
        }

        /// <summary>
        /// HFSM機構を停止します
        /// </summary>
        public void Stop()
        {
            BeginOperation();

            try
            {
                if (!IsStarted)
                    return;

                // 末端からルートまで退出する。
                while (_activePath.Count > 0)
                    ExitLeaf();

                IsStarted = false;
                _pendingEvents.Clear();
            }
            finally
            {
                _isExecuting = false;
            }
        }

        // 以下、ステート側から状態遷移処理を行うために追加
        // 複数のイベントを処理できるように、FIFOを採用しています
        private readonly Queue<TEvent> _pendingEvents = new Queue<TEvent>(16);

        private bool _isProcessingPendingEvents;

        // このメソッドの要約はInterfaceにあります
        public void Post(TEvent input)
        {
            if (!IsStarted)
                return;

            // Dispatchせず、予約だけ行う
            _pendingEvents.Enqueue(input);
        }

        /// <summary>
        /// 予約されたイベントを処理します
        /// </summary>
        /// <param name="maxEvents">処理できる最大イベント数</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">更新や遷移処理中に再度このメソッドが呼ばれた場合に出力</exception>
        /// <exception cref="ArgumentOutOfRangeException">maxEventsが0以下の場合に出力</exception>
        public int ProcessPendingEvents(int maxEvents = 32)
        {
            // 更新・遷移の途中でキューを処理しない
            if (_isExecuting || _isProcessingPendingEvents)
            {
                throw new InvalidOperationException(
                    "イベント処理への再入は禁止しています。");
            }

            if (maxEvents < 1)
                throw new ArgumentOutOfRangeException(nameof(maxEvents));

            EnsureStarted();

            _isProcessingPendingEvents = true;

            try
            {
                int processed = 0;

                // Dispatch中のOnEnterなどからPostされたイベントも、
                // 上限に達するまでは同じ処理単位で受け付ける
                while (processed < maxEvents && _pendingEvents.Count > 0)
                {
                    TEvent input = _pendingEvents.Dequeue();

                    Dispatch(input);
                    processed++;
                }

                // 上限を超えた分は、次の呼び出しへ持ち越す
                return processed;
            }
            finally
            {
                _isProcessingPendingEvents = false;
            }
        }
    }
}
