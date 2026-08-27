// RETIRED - PLEASE DELETE THIS FILE AND ITS .meta.
//
// This file used to build the placeholder store button that core SDK setup put on menu.unity.
// That button was always temporary: it was the plain way into the store until the positioned,
// art-directed entry arrived as a server-configured touchpoint.
//
// It has now arrived. The "home" touchpoint lands on the same menu screen, so keeping this
// button as well would ship TWO store entrances competing on one screen. See
// PlaySuperTouchpointHome.cs, which replaces it.
//
// The one thing this file owned besides the button - the OnStoreOpened / OnStoreClosed audio
// ducking that pauses menu music while the store WebView is up - was MOVED into
// PlaySuperTouchpointHome rather than dropped.
//
// Why a tombstone instead of an actual deletion: the PlaySuper tooling that opened this pull
// request can add and replace files, but cannot delete them. This file is now comments only, so
// it compiles harmlessly, but it is dead weight. Run:
//
//     git rm Assets/Scripts/PlaySuper/PlaySuperStoreEntry.cs \
//            Assets/Scripts/PlaySuper/PlaySuperStoreEntry.cs.meta
//
// on this branch, or immediately after merging it.
