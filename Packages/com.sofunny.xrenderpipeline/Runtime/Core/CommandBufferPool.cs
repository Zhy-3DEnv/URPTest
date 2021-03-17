using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Events;

namespace Framework.XRenderPipeline {

    public static class CommandBufferPool {

        static Stack<CommandBuffer> s_CommandBufferPool = new Stack<CommandBuffer>();

        public static CommandBuffer Get() {
            CommandBuffer cmdbuf;
            if (s_CommandBufferPool.Count == 0) {
                cmdbuf = new CommandBuffer();
            } else {
                cmdbuf = s_CommandBufferPool.Pop();
            }
            cmdbuf.name = "Unnamed Command Buffer";
            return cmdbuf;
        }

        public static CommandBuffer Get(string name) {
            CommandBuffer cmdbuf;
            if (s_CommandBufferPool.Count == 0) {
                cmdbuf = new CommandBuffer();
            } else {
                cmdbuf = s_CommandBufferPool.Pop();
            }
            cmdbuf.name = name;
            return cmdbuf;
        }

        public static void Release(CommandBuffer cmdbuf) {
#if UNITY_EDITOR
            if (s_CommandBufferPool.Contains(cmdbuf)) {
                Debug.LogError("Trying to release a commandbuffer that is already released to pool");
            }
#endif
            cmdbuf.Clear();
            s_CommandBufferPool.Push(cmdbuf);
        }
    }

}

