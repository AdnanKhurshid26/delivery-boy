// RETIRED - safe to delete this file and its .meta.
//
// This held PlaySuperStoreButton: the plain, unstyled store entry point that core
// setup built at runtime on menu.unity so the store was reachable while the core loop
// was verified. It always said it was temporary and would be replaced by a touchpoint.
//
// That touchpoint has landed. "home-2" is the art-directed store entry point on the
// same menu screen, rendered by PlaySuperTouchpointMenu, so keeping this would put two
// competing store entrances on one screen. The type is gone and nothing spawns.
//
// The one thing it owned beyond the button was pausing the AudioListener while the
// store WebView was in front of the player. That was NOT dropped: it lives in
// PlaySuperTouchpointWidget, which also releases the pause in OnDisable so a scene
// change with the store open cannot leave the game silent.
