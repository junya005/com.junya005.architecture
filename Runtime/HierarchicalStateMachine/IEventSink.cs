namespace Junya005.Architecture
{
    /// <summary>
    /// ステートからイベントを予約するためのインターフェース
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    public interface IEventSink<TEvent>
    {
        /// <summary>
        /// イベントを予約します
        /// </summary>
        /// <param name="input">予約するイベント名</param>
        void Post(TEvent input);
    }
}
