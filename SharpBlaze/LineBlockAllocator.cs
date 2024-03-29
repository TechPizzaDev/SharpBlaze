using System;
using System.Runtime.InteropServices;

namespace SharpBlaze;

public unsafe partial class LineBlockAllocator
{
    ~LineBlockAllocator()
    {
        Arena* p = mAllArenas;

        while (p != null)
        {
            Arena* next = p->Links.NextAll;

            NativeMemory.Free(p);

            p = next;
        }
    }


    public partial void Clear()
    {
        Arena* l = null;

        Arena* p = mAllArenas;

        while (p != null)
        {
            Arena* next = p->Links.NextAll;

            p->Links.NextFree = l;

            l = p;

            p = next;
        }

        mCurrent = null;
        mEnd = null;
        mFreeArenas = l;
    }


    private partial void NewArena()
    {
        Arena* p = mFreeArenas;

        if (p != null)
        {
            mFreeArenas = p->Links.NextFree;
        }
        else
        {
            p = (Arena*) (NativeMemory.Alloc((nuint) sizeof(Arena)));

            p->Links.NextAll = mAllArenas;

            mAllArenas = p;
        }

        p->Links.NextFree = null;

        mCurrent = p->Memory + sizeof(ArenaLinks);
        mEnd = p->Memory + Arena.Size - Math.Max(
            sizeof(LineArrayX32Y16Block),
            Math.Max(
                sizeof(LineArrayX16Y16Block), 
                sizeof(LineArrayTiledBlock)));
    }
}