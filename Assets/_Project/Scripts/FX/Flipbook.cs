using System;
using UnityEngine;

namespace TenCandles
{
    // Plays a sprite sequence once, then hands itself back to its pool.
    public class Flipbook : MonoBehaviour
    {
        SpriteRenderer sr;
        Sprite[] frames;
        float fps, started;
        Action<Flipbook> release;

        public void Build(int sortingOrder)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
        }

        public void Play(Sprite[] sequence, Vector3 position, float scale, float framesPerSecond, Action<Flipbook> onDone)
        {
            frames = sequence;
            fps = framesPerSecond;
            release = onDone;
            started = Time.time;
            transform.position = position;
            transform.localScale = Vector3.one * scale;
            sr.flipX = UnityEngine.Random.value < 0.5f;
            sr.sprite = frames[0];
        }

        void Update()
        {
            int frame = Mathf.FloorToInt((Time.time - started) * fps);
            if (frame >= frames.Length)
            {
                release?.Invoke(this);
                return;
            }
            sr.sprite = frames[frame];
        }
    }
}
