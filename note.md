# Note for updating patches

## `ChangeSubclass`
Found by search string "CS2 does not support changing entity subclasses." and patch the jump condition

As of the 2026-09-28 update this string no longer exists in either binary, and the restricting branch appears to be gone from the Windows ChangeSubclass (only the generic entity check and the "subclass exists and differs" checks remain), so the patch is disabled by default.

## `EmitSoundVolumeFix`
Found in the internal function called inside "EmitSoundFilter",

at the bottom, theres a hash `0x2D8464AF` ( which is the murmurhash2 hash of "public.volume_atten" with seed 0x31415926 )

patch to change the hash to `0xBD6054E9` ( the murmurhash2 hash of "public.volume" with seed 0x31415926 )

## `TeammateCanBlockGrenade`
Found in a function that modified `CBaseGrenadeProjectile::m_bHasEverHitEnemy`,

get the offset of `m_bHasEverHitEnemy` in schema system, and use `Find immediate value` of IDA to find that function,

then find the similar code like this:
```
if ( (*(unsigned __int8 (__fastcall **)(_QWORD))(*(_QWORD *)v13 + 1376i64))(*v12)
    && (*(unsigned __int8 (__fastcall **)(__int64))(*(_QWORD *)v13 + 3376i64))(v13)
    && sub_1806F4E40(v2, v13) )
  {
    projectile->m_bHasEverHitEnemy = 1;
```

The third condition is the function that return if two player is not teammate
patch that condition to remove the check

If the compiler emits a near `jne` (`0F 85 rel32`) instead of a short one (`75 rel8`), patch it to `90 E9` (nop + `jmp rel32`) so the jump target stays the same.