namespace OSTES.Chart

{

    /// <summary>
    /// 마우스로 선택한 최근 두 포인트를 보관한다.
    /// SelectionPin Marker는 기존 UI 로직이 관리하고, Delta Text는 이 버퍼의 좌표를 기준으로 계산한다.
    /// </summary>
    internal sealed class RecentDeltaPointBuffer<T>

    {

        /** @brief 최근 선택 포인트 2개를 저장하는 고정 버퍼 */

        private readonly T[] _points = new T[2];

        /** @brief 다음 클릭 좌표가 저장될 위치 */

        private int _nextWriteIndex;

        /** @brief 현재 버퍼에 저장된 유효 포인트 개수 */

        private int _count;

        /** @brief Delta 계산이 가능한 상태인지 여부 */

        public bool HasTwoPoints => _count == _points.Length;

        /** @brief 현재 Delta 계산 기준 중 오래된 포인트 */

        public T OlderPoint => _points[_nextWriteIndex];

        /** @brief 현재 Delta 계산 기준 중 최신 포인트 */

        public T NewerPoint => _points[(_nextWriteIndex + 1) % _points.Length];

        /// <summary>
        /// 새 선택 좌표를 추가한다.
        /// 2개를 초과하면 가장 오래된 좌표를 덮어써서 항상 최신 두 점만 유지한다.
        /// </summary>
        public void Push(T point)

        {

            _points[_nextWriteIndex] = point;

            _nextWriteIndex = (_nextWriteIndex + 1) % _points.Length;

            if (_count < _points.Length)

            {

                _count++;

            }

        }

        /** @brief 선택 포인트 기록을 초기화한다. */

        public void Clear()

        {

            for (var i = 0; i < _points.Length; i++)

            {

                _points[i] = default;

            }

            _nextWriteIndex = 0;

            _count = 0;

        }

    }

}
