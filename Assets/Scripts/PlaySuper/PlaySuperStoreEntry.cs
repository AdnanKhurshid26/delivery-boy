// REMOVED - please delete this file and its .cs.meta.
//
// PlaySuperStoreEntry built a plain placeholder "REWARDS" button in the corner
// of the menu scene. It existed only until the art-directed store entry arrived
// as a touchpoint, which it now has: see PlaySuperTouchpoint_Home.cs, which
// renders the "home" touchpoint into the menu's button stack.
//
// Keeping both would put two store entrances on the same screen. The one thing
// this file owned besides the button - pausing audio and timescale across
// OnStoreOpened / OnStoreClosed - moved into PlaySuperTouchpoint_Home rather
// than being dropped.
//
// This is left as an empty file only because the tool that opened this pull
// request can add and replace files but not delete them.
