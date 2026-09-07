// RETIRED - safe to delete this file and its .meta.
//
// This was the temporary store entry point from PlaySuper core setup: a plain REWARDS button
// built at runtime on the menu screen, which existed only until a real touchpoint arrived.
// That touchpoint is now live as "home-2" and lands on the same screen, so this placeholder is
// gone - shipping both would put two store entrances on one menu.
//
// The store-open/close audio ducking this file used to own now lives in
// PlaySuperTouchpointWidget, shared by both touchpoint renderers.
//
// The file is emptied rather than removed because the tool that opened this pull request can
// add and replace files but not delete them. Deleting these two paths is the one manual step:
//
//     Assets/Scripts/PlaySuper/PlaySuperStoreEntry.cs
//     Assets/Scripts/PlaySuper/PlaySuperStoreEntry.cs.meta
