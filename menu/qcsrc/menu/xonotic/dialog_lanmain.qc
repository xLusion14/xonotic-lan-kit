#include "dialog_lanmain.qh"

#include "tabcontroller.qh"
#include "button.qh"
#include "commandbutton.qh"
#include "mainwindow.qh"
#include "dialog_multiplayer_join.qh"
#include "dialog_multiplayer_profile.qh"

#include "dialog_settings_video.qh"
#include "dialog_settings_effects.qh"
#include "dialog_settings_audio.qh"
#include "dialog_settings_game.qh"
#include "dialog_settings_input.qh"
#include "dialog_settings_user.qh"
#include "dialog_settings_misc.qh"

void InstantAction_LoadMap(entity btn, entity dummy);  // dialog_singleplayer.qc

// Same layout as XonoticSettingsDialog_fill, one row shorter to make room for the Close button.
void XonoticLanSettingsDialog_fill(entity me)
{
	entity mc, e;
	mc = makeXonoticTabController(me.rows - 3.5);
	me.TR(me);
		me.TD(me, 1, 2, mc.makeTabButton(mc, _("Video"),   makeXonoticVideoSettingsTab()));
		me.TD(me, 1, 2, mc.makeTabButton(mc, _("Effects"), makeXonoticEffectsSettingsTab()));
		me.TD(me, 1, 2, mc.makeTabButton(mc, _("Audio"),   makeXonoticAudioSettingsTab()));
	me.TR(me);
		me.TD(me, 1, 1.5, mc.makeTabButton(mc, _("Game"),    NEW(XonoticGameSettingsTab)));
		me.TD(me, 1, 1.5, mc.makeTabButton(mc, _("Input"),   makeXonoticInputSettingsTab()));
		me.TD(me, 1, 1.5, mc.makeTabButton(mc, _("User"),    makeXonoticUserSettingsTab()));
		me.TD(me, 1, 1.5, mc.makeTabButton(mc, _("Misc"),    makeXonoticMiscSettingsTab()));
	me.gotoRC(me, 2.5, 0);
		me.TD(me, me.rows - 3.5, me.columns, mc);
	me.gotoRC(me, me.rows - 1, 2);
		me.TD(me, 1, 2, e = makeXonoticButton(_("Close"), '0 0 0'));
			e.onClick = Dialog_Close;
			e.onClickEntity = me;
}

float XonoticLanSettingsDialog_mousePress(entity me, vector pos)
{
	// pos is in the dialog's own 0..1 box; a click anywhere outside it closes the pop-up
	if (pos.x < 0 || pos.x > 1 || pos.y < 0 || pos.y > 1)
	{
		m_play_click_sound(MENU_SOUND_CLOSE);
		me.close(me);
		return true;
	}
	return SUPER(XonoticLanSettingsDialog).mousePress(me, pos);
}

void XonoticGearButton_draw(entity me)
{
	SUPER(XonoticGearButton).draw(me);
	// item coordinates run 0..1 on both axes, so scale the icon's width by the button's height/width ratio
	float h = 0.8, w = h * me.size.y / me.size.x;
	draw_Picture(eX * (0.5 - 0.5 * w) + eY * (0.5 - 0.5 * h), "/gfx/lanmenu/gear", eX * w + eY * h, '1 1 1', 1);
}

void XonoticLanMainDialog_fill(entity me)
{
	entity mc, e;
	mc = makeXonoticTabController(me.rows - 1);
	float gear = 0.4;  // columns

	me.TR(me);
		me.TD(me, 1, 1.2, e = mc.makeTabButton_T(mc, _("Games on your network"), makeXonoticServerListTab(),
			_("Servers found on this network")));
		me.TD(me, 1, 1, e = mc.makeTabButton_T(mc, _("Your player"), makeXonoticProfileTab(),
			_("Your name, model and colors")));
	me.gotoRC(me, 0, 2.5);
		me.TD(me, 1, 1.2, e = makeXonoticButton_T(_("Practice vs bots"), '0 0 0',
			_("Play a random map against bots on your own")));
			e.onClick = InstantAction_LoadMap;
			e.onClickEntity = NULL;
	me.gotoRC(me, 0, me.columns - 0.8 - 0.1 - gear);
		me.TD(me, 1, gear, e = NEW(XonoticGearButton));
			e.configureXonoticButton(e, "", '0 0 0', _("Settings"));
			e.onClick = DialogOpenButton_Click;
			e.onClickEntity = main.lanSettingsDialog;
	me.gotoRC(me, 0, me.columns - 0.8);
		me.TD(me, 1, 0.8, e = makeXonoticCommandButton(_("Quit"), '1 0 0', "menu_showquitdialog", 0));

	me.gotoRC(me, 1, 0);
		me.TD(me, me.rows - 1, me.columns, mc);
}

float XonoticLanMainDialog_keyDown(entity me, float key, float ascii, float shift)
{
	// Dialog swallows Escape when it isn't closable; let it through so Escape still leaves the menu during a match.
	if (key == K_ESCAPE) return false;
	return SUPER(XonoticLanMainDialog).keyDown(me, key, ascii, shift);
}
