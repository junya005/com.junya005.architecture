#nullable enable

using System;
using System.Collections.Generic;

namespace Junya005.Architecture
{
    /// <summary>
    /// HFSMにおいての状態を表すクラス
    /// </summary>
    /// <typeparam name="TContext">状態機械で使いまわすコンテキスト</typeparam>
    /// <typeparam name="TEvent">状態機械で使いまわすイベント</typeparam>
    public abstract class State<TContext, TEvent>
        where TEvent : notnull
    {
        private readonly Dictionary<TEvent, State<TContext, TEvent>>
            _transitions = new();

        public string Name { get; }

        public State<TContext, TEvent>? Parent { get; }

        public State<TContext, TEvent>? InitialChild { get; private set; }

        /// <summary>
        /// 状態を初期化します
        /// </summary>
        /// <param name="name">状態名</param>
        /// <param name="parent">状態の親</param>
        protected State(
            string name,
            State<TContext, TEvent>? parent = null)
        {
            Name = name;
            Parent = parent;
        }

        /// <summary>
        /// 自身の子となる状態を設定します。
        /// </summary>
        /// <param name="child">子に指定したい状態</param>
        /// <exception cref="ArgumentNullException">指定された状態が空の場合に出力</exception>
        /// <exception cref="ArgumentException">指定された子のParentが自身と一致しない場合に出力</exception>
        public void SetInitialChild(State<TContext, TEvent> child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            if (!ReferenceEquals(child.Parent, this))
            {
                throw new ArgumentException(
                    "初期子状態には、直接の子を指定してください。",
                    nameof(child));
            }

            InitialChild = child;
        }

        /// <summary>
        /// イベントが発行された際に遷移可能な状態を追加します
        /// </summary>
        /// <param name="input">遷移条件となるイベント</param>
        /// <param name="target">遷移先</param>
        /// <exception cref="ArgumentNullException">遷移先の指定が空の場合に出力</exception>
        /// <exception cref="NotSupportedException">遷移先の指定がこの状態の親子関係や自分自身であった場合に出力</exception>
        /// <exception cref="ArgumentException">指定された状態が現在とは別のHFSMに属する場合に出力</exception>
        public void AddTransition(
            TEvent input,
            State<TContext, TEvent> target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            // 自己遷移・祖先子孫間の遷移は、この実装では扱わない。
            if (IsAncestorOrSelf(this, target) ||
                IsAncestorOrSelf(target, this))
            {
                throw new NotSupportedException(
                    "自己遷移・祖先子孫間の遷移は未対応です。");
            }

            if (!ReferenceEquals(GetRoot(this), GetRoot(target)))
            {
                throw new ArgumentException(
                    "別の状態ツリーには遷移できません。",
                    nameof(target));
            }

            // 同一状態に同じイベントを重複登録すると例外になる。
            _transitions.Add(input, target);
        }

        /// <summary>
        /// 自身から遷移可能なイベントの取得を試行します。
        /// </summary>
        /// <remarks>
        /// この状態の親子はそもそも登録できないため除外されます。
        /// </remarks>
        /// <param name="input">遷移条件となるイベント</param>
        /// <param name="target">out 遷移先の状態objが格納されます</param>
        /// <returns>取得に成功したか</returns>
        internal bool TryGetTransition(
            TEvent input,
            out State<TContext, TEvent>? target)
        {
            return _transitions.TryGetValue(input, out target);
        }

        /// <summary>
        /// 状態が遷移し、この状態に入った時に呼ばれます。
        /// </summary>
        /// <param name="context">階層で使いまわしているコンテキスト</param>
        public virtual void OnEnter(TContext context) { }

        /// <summary>
        /// この状態である場合（親子含む階層に属する場合）に毎フレーム呼ばれます。
        /// </summary>
        /// <param name="context">階層で使いまわしているコンテキスト</param>
        /// <param name="deltaTime">デルタタイム（概念は調べてください）</param>
        public virtual void OnTick(TContext context, float deltaTime) { }

        /// <summary>
        /// この状態を抜けるときに呼ばれます。
        /// </summary>
        /// <param name="context">階層で使いまわしているコンテキスト</param>
        public virtual void OnExit(TContext context) { }

        /// <summary>
        /// 自身が指定された状態の祖先状態自身もしくはその子であるかを判定します。
        /// </summary>
        /// <param name="ancestor">比較する状態</param>
        /// <param name="state">調査を開始する状態</param>
        /// <returns></returns>
        private static bool IsAncestorOrSelf(
            State<TContext, TEvent> ancestor,
            State<TContext, TEvent> state)
        {
            for (State<TContext, TEvent>? current = state;
                 current is not null;
                 current = current.Parent)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 状態が属する階層のルートを取得します
        /// </summary>
        /// <param name="state">調べる状態</param>
        /// <returns>ルートの状態</returns>
        private static State<TContext, TEvent> GetRoot(
            State<TContext, TEvent> state)
        {
            while (state.Parent is not null)
                state = state.Parent;

            return state;
        }

        /// <summary>
        /// オブジェクトの名前を文字列で取得します。
        /// </summary>
        public override string ToString() => Name;
    }
}
