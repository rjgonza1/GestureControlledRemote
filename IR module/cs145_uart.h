#ifndef CS145_UART 
#define CS145_UART

#include <avr/io.h>

#define F_CPU 16000000UL
#define BAUD 9600
#define MYUBRR F_CPU/16/BAUD-1

void UART_init(unsigned int ubrr) {
	UBRR0H = (unsigned char) (ubrr >> 8);
	UBRR0L = (unsigned char) ubrr;
	UCSR0B = (1 << RXEN0) | (1 << TXEN0);
}

void USART_transmit(char *data) {
	for(int index = 0; data[index] != '\0'; ++index) {
		while(!(UCSR0A & (1 << UDRE0)));
		UDR0 = data[index];
	}
}

unsigned char USART_receive(void) {
	while(!(UCSR0A & (1 << RXC0)));
	return UDR0;
}
#endif //CS145_UART