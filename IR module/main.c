/*
 * main.c
 *
 * Created: 5/24/2017 1:06:00 AM
 * Author : Marcos Avila
 */

#include <avr/io.h>
#include <avr/interrupt.h>
#include <stdio.h>

#include "cs145_uart.h"
#include <util/delay.h>

void ir_module_init(void);
void send_zero(void);
void send_one(void);
void volume_up(void);
void volume_down(void);
void channel_up(void);
void channel_down(void);
void power_on(void);

int main(void) {
	UART_init(MYUBRR);
	char command;
	char message[16];
	ir_module_init();
	for (;;) {
		command = USART_receive();
		switch(command) {
		case ' ':
			power_on();
			sprintf((char *)&message, "power on\n\r");
			break;
		case 'w':
			volume_up();
			sprintf((char *)&message, "volume up\n\r");
			break;
		case 's':
			volume_down();
			sprintf((char *)&message, "volume down\n\r");
			break;
		case 'd':
			channel_up();
			sprintf((char *)&message, "channel up\n\r");
			break;
		case 'a':
			channel_down();
			sprintf((char *)&message, "channel down\n\r");
			break;
		default:
			sprintf((char *)&message, "incorrect input\n\r");
			break;
		}
		USART_transmit(message);
	}
}

void volume_up(void)
{
	// Binary code for volume up: 11000000010000
	send_one();
	send_one();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_one();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	_delay_ms(80);	
}

void volume_down(void)
{
	// Binary code for volume up: 11100000010001
	send_one();
	send_one();
	send_one();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_one();
	send_zero();
	send_zero();
	send_zero();
	send_one();
	_delay_ms(80);
}

void channel_up(void)
{
	// Binary code for volume up: 11000000010000
	send_one();
	send_one();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_one();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	_delay_ms(80);
}

void channel_down(void)
{
	// Binary code for volume up: 11100000100001
	send_one();
	send_one();
	send_one();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_one();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_one();
	_delay_ms(80);
}

void power_on(void)
{
	// Binary code for power on: 11000000001100
	send_one();
	send_one();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_zero();
	send_one();
	send_one();
	send_zero();
	send_zero();
	_delay_ms(80);	
}

void ir_module_init(void) 
{
	PCICR |= (1 << PCIE0);
	PCMSK0 |= (1 << PCINT7);
	DDRB |= (1 << DDB1) | (1 << DDB2);

	PORTB &= ~(1 << DDB2);

	TCCR1A |= (1 << COM1A0)| (1 << WGM10);
	TCCR1B |= (1 << WGM13) | (1 << CS10);
	OCR1A = 104; // for 38 kHz
	TCNT1 = 0;
}


// Sending 0 and 1 using RC-5 Protocol
void send_zero(void)
{
	PORTB |= 1 << PORTB2;
	_delay_us(872);
	PORTB &= ~(1 << PORTB2);
	_delay_us(872);
}

void send_one(void)
{
	PORTB &= ~(1 << PORTB2);
	_delay_us(872);
	PORTB |= 1 << PORTB2;
	_delay_us(872);
	PORTB &= ~(1 << PORTB2);
}