// RETIRED.
//
// This file used to build the plain placeholder "REWARDS" store button on the menu
// screen during core SDK setup. Its own comment said a PlaySuper touchpoint would
// replace it, and the "home-2" touchpoint now does exactly that on the same screen —
// see PlaySuperTouchpoint_Home2.cs. Shipping both would put two store entrances on
// one menu.
//
// The store-open/close audio ducking this file also owned was NOT dropped: it moved
// into PlaySuperTouchpointWidget.cs, so both touchpoints still pause and resume audio
// around the store WebView.
//
// This file is now inert and can be deleted along with its .meta:
//
//   git rm Assets/Scripts/PlaySuper/PlaySuperStoreEntryPoint.cs \
//          Assets/Scripts/PlaySuper/PlaySuperStoreEntryPoint.cs.meta
