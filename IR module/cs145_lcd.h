#ifndef CS145_LCD
#define CS145_LCD

#include <avr/io.h>
#include <util/delay.h>

void LcdCommandWrite_UpperNibble(unsigned char data) {
	PORTC = (PORTC & 0xf0) | (data >> 4);
	PORTC &= ~(1 << PORTC4);
	PORTC |= 1 << PORTC5;
	_delay_ms(1);
	PORTC &= ~(1 << PORTC5);
	_delay_ms(1);
}

void LcdCommandWrite_LowerNibble(unsigned char data) {
	PORTC = (PORTC & 0xf0) | (data & 0x0f);
	PORTC &= ~(1 << PORTC4);
	PORTC |= 1 << PORTC5;
	_delay_ms(1);
	PORTC &= ~(1 << PORTC5);
	_delay_ms(1);
}

void LcdDataWrite_UpperNibble(unsigned char data) {
	PORTC = (PORTC & 0xf0) | (data >> 4);
	PORTC |= 1 << PORTC4;
	PORTC |= 1 << PORTC5;
	_delay_ms(1);
	PORTC &= ~(1 << PORTC5);
	_delay_ms(1);
}

void LcdDataWrite_LowerNibble(unsigned char data) {
	PORTC = (PORTC & 0xf0) | (data & 0x0f);
	PORTC |= 1 << PORTC4;
	PORTC |= 1 << PORTC5;
	_delay_ms(1);
	PORTC &= ~(1 << PORTC5);
	_delay_ms(1);
}

void LcdCommandWrite(unsigned char data) {
	LcdCommandWrite_UpperNibble(data);
	LcdCommandWrite_LowerNibble(data);
}

void LcdDataWrite(unsigned char data, unsigned char *cursor_idx) {
	if (*cursor_idx == 0x10)
	LcdCommandWrite(0xc0);
	else if (*cursor_idx == 0x20) {
		LcdCommandWrite(0x02);
		*cursor_idx = 0;
	}
	LcdDataWrite_UpperNibble(data);
	LcdDataWrite_LowerNibble(data);
	++*cursor_idx;
}

void LCD_Init() {
	LcdCommandWrite_UpperNibble(0x30);
	_delay_ms(4.1);
	LcdCommandWrite_UpperNibble(0x30);
	_delay_us(100);
	LcdCommandWrite_UpperNibble(0x30);
	LcdCommandWrite_UpperNibble(0x20);

	LcdCommandWrite(0x28);	// function set: 0x28 means,  4-bit interface, 2 lines, 5x8 font
	LcdCommandWrite(0x08);	// display control: turn display off, cursor off, no blinking
	LcdCommandWrite(0x01);	// clear display, set address counter  to zero
	LcdCommandWrite(0x06);	// entry mode set:
	LcdCommandWrite(0x0f);	// display on, cursor on, cursor blinking
	_delay_ms(120);
}
#endif //CS145_LCD