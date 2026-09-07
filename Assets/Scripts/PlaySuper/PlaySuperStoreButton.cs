// RETIRED - safe to delete this file and its .meta.
//
// This used to build a plain placeholder "REWARDS" button on the menu screen, as a
// temporary way into the PlaySuper store while the core loop was being verified. Its own
// comment said a touchpoint would replace it, and the "home-2" touchpoint now does:
// see PlaySuperTouchpoint_Home2.cs. Keeping both would have put two store entrances on
// one menu screen.
//
// The store open/close audio ducking this file also owned has moved into
// PlaySuperTouchpointRenderer, so no behaviour was lost.
//
// The file is emptied rather than removed only because the PlaySuper connector can add
// and replace files but not delete them. A comment-only .cs compiles and does nothing.
// Please delete Assets/Scripts/PlaySuper/PlaySuperStoreButton.cs and
// Assets/Scripts/PlaySuper/PlaySuperStoreButton.cs.meta after merging.
